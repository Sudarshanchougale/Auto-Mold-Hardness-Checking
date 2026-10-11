import bpy
import math
import random
from mathutils import Matrix, Vector

# =====================================================================
# 0. USER SETTINGS
# =====================================================================
ARM_SCALE = 1.4            # uniform scale of the arm so it can reach every cavity
BASE_X = -6.1              # rail position (world X) beside the mold
HUB_LEAN_DEG = 40.0        # forearm lean toward +X (keeps the wrist inside the cavity opening)
H2_LEAN_DEG = 20.0         # more upright forearm for the right-wall (+X) probe, so it clears the cavity rim
TOOL_LEN_U = 0.60          # j4 pivot -> needle tip, in UNSCALED arm units (was ~1.8)

FPS = 24
SPEED = 1.0                # 1.0 = normal, 2.0 = twice as fast
DWELL_S = 1.2              # time the needle stays on each point
TRAVEL_PZ = 2.8            # wrist height used for travelling above the mold (world Z)
STANDOFF_V = 0.12          # distance above the point where the slow vertical approach starts
STANDOFF_H = 0.15          # distance from the wall where the slow horizontal approach starts

HARD_MIN, HARD_MAX = 75.0, 95.0    # placeholder acceptance window (mold hardness)
SEED = 7                           # same seed -> same simulated readings
SIMULATE_FAILURE = False           # True -> one point gets a below-minimum reading
FAIL_AT = (3, 'H1')                # (sub-mold index 0..4, point) used when SIMULATE_FAILURE
AUTOPLAY = False

# =====================================================================
# 1. CONSTANTS FROM YOUR ORIGINAL SCENE
# =====================================================================
S = 0.749505                              # mold scale factor
frame_loc = (0.0, -6.071907, 0.186715)
sand_loc = (0.0, -6.071907, 0.261666)
FLOOR_TOP = -1.15
R90 = math.radians(90)
AX_X = (0, R90, 0)
AX_Y = (R90, 0, 0)
SIDE = -1            # detail side on the arm (-1 = local -X = faces the camera). Keep -1.

K = ARM_SCALE
RAIL_H = 0.12
Z_C = FLOOR_TOP + RAIL_H + 1.15 * K        # world Z of the carriage origin (unscaled z=0)
Z_J1 = Z_C + 1.15 * K                      # world Z of the shoulder joint
L1, L2, L3 = 2.8 * K, 2.5 * K, 1.2 * K     # scaled link lengths
LT = TOOL_LEN_U * K                        # scaled pivot->tip length

sand_top = sand_loc[2] + 1.0 * S           # top face of the sand
frame_top = frame_loc[2] + 1.1 * S
Z_PIT = sand_loc[2] - 0.2 * S              # floor of the deep pit inside each cavity
Z_LEDGE = sand_loc[2] + 0.2 * S            # floor of the wide pocket
Z_WALL = Z_LEDGE + 0.34                    # height of the side-wall test points
WALL_HALF = 1.0 * S                        # wide pocket half width (walls at cx +/- S)

SUBMOLDS = [("SubMold_1", -2.2, 2.6), ("SubMold_2", 2.2, 2.6), ("SubMold_3", 0.0, 0.0),
            ("SubMold_4", -2.2, -2.6), ("SubMold_5", 2.2, -2.6)]

def sm_center(i):
    return SUBMOLDS[i][1] * S, SUBMOLDS[i][2] * S + sand_loc[1]

# =====================================================================
# 2. SCENE CLEANUP & ENGINE SETUP (version safe)
# =====================================================================
def _remove_old_handlers():
    for lst in (bpy.app.handlers.frame_change_post, bpy.app.handlers.frame_change_pre):
        for h in list(lst):
            if getattr(h, '__name__', '') == 'zanvar_frame_handler':
                lst.remove(h)

def cleanup_scene():
    _remove_old_handlers()
    if bpy.context.object is not None and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.curves, bpy.data.lights,
                 bpy.data.cameras, bpy.data.materials, bpy.data.actions):
        for item in list(coll):
            coll.remove(item)

cleanup_scene()
scene = bpy.context.scene

for eng in ('BLENDER_EEVEE_NEXT', 'BLENDER_EEVEE'):
    try:
        scene.render.engine = eng
        break
    except TypeError:
        continue

world = scene.world
if world is None:
    world = bpy.data.worlds.new("Foundry_World")
    scene.world = world
try:
    world.use_nodes = True
except Exception:
    pass
bg_node = world.node_tree.nodes.get("Background") if world.node_tree else None
if bg_node:
    bg_node.inputs[0].default_value = (0.02, 0.03, 0.05, 1.0)

eevee = scene.eevee
if hasattr(eevee, 'use_raytracing'):
    eevee.use_raytracing = True
if hasattr(eevee, 'use_ssr'):
    eevee.use_ssr = True
    eevee.use_ssr_refraction = True
if hasattr(eevee, 'use_gtao'):
    eevee.use_gtao = True

for _vt in ('AgX', 'Filmic', 'Standard'):
    try:
        scene.view_settings.view_transform = _vt
        break
    except TypeError:
        continue

scene.render.fps = FPS

# =====================================================================
# 3. PROCEDURAL MATERIALS
# =====================================================================
def new_mat(name):
    mat = bpy.data.materials.new(name=name)
    try:
        mat.use_nodes = True
    except Exception:
        pass
    nodes = mat.node_tree.nodes
    return mat, nodes, mat.node_tree.links, nodes.get("Principled BSDF")

def set_input(bsdf, names, value):
    for n in names:
        if n in bsdf.inputs:
            bsdf.inputs[n].default_value = value
            return

def create_basic_mat(name, color, roughness=0.3, metallic=0.0, emission=None, strength=0.0):
    mat, nodes, links, bsdf = new_mat(name)
    if bsdf:
        bsdf.inputs['Base Color'].default_value = color
        bsdf.inputs['Roughness'].default_value = roughness
        bsdf.inputs['Metallic'].default_value = metallic
        if emission is not None:
            set_input(bsdf, ('Emission Color', 'Emission'), emission)
            set_input(bsdf, ('Emission Strength',), strength)
    mat.diffuse_color = color
    return mat

def create_powder_coat():
    mat, nodes, links, bsdf = new_mat("Mat_Orange_PowderCoat")
    bsdf.inputs['Base Color'].default_value = (1.0, 0.18, 0.0, 1.0)
    bsdf.inputs['Metallic'].default_value = 0.2
    mat.diffuse_color = (1.0, 0.18, 0.0, 1.0)
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 350.0
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.2
    ramp.color_ramp.elements[0].color = (0.3, 0.3, 0.3, 1.0)
    ramp.color_ramp.elements[1].position = 0.8
    ramp.color_ramp.elements[1].color = (0.5, 0.5, 0.5, 1.0)
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.15
    bump.inputs['Distance'].default_value = 0.05
    links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    links.new(ramp.outputs['Color'], bsdf.inputs['Roughness'])
    links.new(noise.outputs['Fac'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    return mat

def create_foundry_sand():
    mat, nodes, links, bsdf = new_mat("Mat_FoundrySand")
    bsdf.inputs['Roughness'].default_value = 0.95
    mat.diffuse_color = (0.02, 0.02, 0.02, 1.0)
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 60.0
    noise.inputs['Detail'].default_value = 12.0
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.35
    ramp.color_ramp.elements[0].color = (0.008, 0.008, 0.008, 1.0)
    ramp.color_ramp.elements[1].position = 0.7
    ramp.color_ramp.elements[1].color = (0.035, 0.030, 0.026, 1.0)
    grain = nodes.new('ShaderNodeTexVoronoi')
    grain.inputs['Scale'].default_value = 2500.0
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.9
    bump.inputs['Distance'].default_value = 0.02
    links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    links.new(grain.outputs['Distance'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    return mat

def create_cast_iron():
    mat, nodes, links, bsdf = new_mat("Mat_CastIron")
    bsdf.inputs['Metallic'].default_value = 0.8
    mat.diffuse_color = (0.12, 0.1, 0.09, 1.0)
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 4.0
    noise.inputs['Detail'].default_value = 15.0
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.4
    ramp.color_ramp.elements[0].color = (0.1, 0.1, 0.1, 1.0)
    ramp.color_ramp.elements[1].position = 0.7
    ramp.color_ramp.elements[1].color = (0.25, 0.15, 0.1, 1.0)
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.4
    bump.inputs['Distance'].default_value = 0.1
    links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    links.new(noise.outputs['Fac'], bsdf.inputs['Roughness'])
    links.new(noise.outputs['Fac'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    return mat

def create_brushed_steel(name="Mat_BrushedSteel", tone=0.55):
    mat, nodes, links, bsdf = new_mat(name)
    bsdf.inputs['Base Color'].default_value = (tone, tone, tone * 1.03, 1.0)
    bsdf.inputs['Metallic'].default_value = 1.0
    mat.diffuse_color = (tone, tone, tone, 1.0)
    coord = nodes.new('ShaderNodeTexCoord')
    mapping = nodes.new('ShaderNodeMapping')
    mapping.inputs['Scale'].default_value = (1.0, 120.0, 1.0)
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 60.0
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.3
    ramp.color_ramp.elements[0].color = (0.22, 0.22, 0.22, 1.0)
    ramp.color_ramp.elements[1].position = 0.7
    ramp.color_ramp.elements[1].color = (0.45, 0.45, 0.45, 1.0)
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.05
    bump.inputs['Distance'].default_value = 0.01
    links.new(coord.outputs['Object'], mapping.inputs['Vector'])
    links.new(mapping.outputs['Vector'], noise.inputs['Vector'])
    links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    links.new(ramp.outputs['Color'], bsdf.inputs['Roughness'])
    links.new(noise.outputs['Fac'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    return mat

def create_concrete(name="Mat_Concrete"):
    mat, nodes, links, bsdf = new_mat(name)
    bsdf.inputs['Roughness'].default_value = 0.85
    mat.diffuse_color = (0.1, 0.1, 0.1, 1.0)
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 5.0
    noise.inputs['Detail'].default_value = 14.0
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.35
    ramp.color_ramp.elements[0].color = (0.05, 0.05, 0.052, 1.0)
    ramp.color_ramp.elements[1].position = 0.7
    ramp.color_ramp.elements[1].color = (0.13, 0.13, 0.135, 1.0)
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.25
    bump.inputs['Distance'].default_value = 0.05
    links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    links.new(noise.outputs['Fac'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    return mat

mat_orange = create_powder_coat()
mat_dark = create_basic_mat("Mat_DarkMetal", (0.05, 0.05, 0.06, 1.0), 0.5, 0.85)
mat_chrome = create_basic_mat("Mat_Chrome", (0.9, 0.9, 0.95, 1.0), 0.05, 1.0)
mat_iron = create_cast_iron()
mat_sand = create_foundry_sand()
mat_floor = create_concrete("Mat_ConcreteFloor")
mat_blue = create_basic_mat("Mat_Blue", (0.05, 0.3, 0.9, 1.0), 0.2, 0.3)
mat_steel = create_brushed_steel()
mat_rubber = create_basic_mat("Mat_Rubber", (0.012, 0.012, 0.013, 1.0), 0.75, 0.0)
mat_hazard = create_basic_mat("Mat_SafetyYellow", (0.95, 0.68, 0.0, 1.0), 0.45, 0.0)
mat_label = create_basic_mat("Mat_AluLabel", (0.8, 0.8, 0.82, 1.0), 0.3, 1.0)
mat_chalk = create_basic_mat("Mat_Chalk", (0.85, 0.85, 0.8, 1.0), 1.0, 0.0)
mat_led = create_basic_mat("Mat_LED_Green", (0.05, 0.9, 0.2, 1.0), 0.3, 0.0,
                           emission=(0.1, 1.0, 0.25, 1.0), strength=6.0)
mat_led_cyan = create_basic_mat("Mat_LED_Cyan", (0.1, 0.8, 0.9, 1.0), 0.3, 0.0,
                                emission=(0.1, 0.9, 1.0, 1.0), strength=5.0)
mat_pending = create_basic_mat("Mat_Point_Pending", (1.0, 0.55, 0.0, 1.0), 0.3, 0.0,
                               emission=(1.0, 0.5, 0.0, 1.0), strength=4.0)
mat_ok = create_basic_mat("Mat_Point_OK", (0.05, 0.9, 0.2, 1.0), 0.3, 0.0,
                          emission=(0.1, 1.0, 0.25, 1.0), strength=5.0)
mat_bad = create_basic_mat("Mat_Point_FAIL", (0.9, 0.05, 0.05, 1.0), 0.3, 0.0,
                           emission=(1.0, 0.05, 0.05, 1.0), strength=6.0)
mat_screen = create_basic_mat("Mat_Screen", (0.01, 0.015, 0.02, 1.0), 0.4, 0.0)
mat_text = create_basic_mat("Mat_ScreenText", (0.8, 0.95, 1.0, 1.0), 0.5, 0.0,
                            emission=(0.7, 0.9, 1.0, 1.0), strength=2.5)

# =====================================================================
# 4. HELPER FUNCTIONS
# =====================================================================
def smooth(obj, angle_deg=35.0):
    try:
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        if bpy.app.version >= (4, 1, 0):
            bpy.ops.object.shade_auto_smooth(angle=math.radians(angle_deg))
        else:
            bpy.ops.object.shade_smooth()
            obj.data.use_auto_smooth = True
            obj.data.auto_smooth_angle = math.radians(angle_deg)
    except Exception:
        try:
            bpy.ops.object.shade_smooth()
        except Exception:
            pass

def add_bevel(obj, width, segments, shade=True):
    bevel = obj.modifiers.new(name="Bevel", type='BEVEL')
    bevel.width = width
    bevel.segments = segments
    if shade:
        smooth(obj)

def parent_keep_transform(child, parent):
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()

def set_pivot(obj, pivot_loc):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    saved_cursor = bpy.context.scene.cursor.location.copy()
    bpy.context.scene.cursor.location = pivot_loc
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.context.scene.cursor.location = saved_cursor
    obj.select_set(False)

def create_box(name, scale, loc, mat, apply_transforms=True):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    if apply_transforms:
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if mat:
        obj.data.materials.append(mat)
    return obj

def join_and_color(target_obj, objects_to_join, color_mat):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects_to_join:
        obj.select_set(True)
    target_obj.select_set(True)
    bpy.context.view_layer.objects.active = target_obj
    bpy.ops.object.join()
    target_obj.data.materials.clear()
    target_obj.data.materials.append(color_mat)
    return target_obj

def create_joint(name, radius, depth, loc, mat):
    bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=radius, depth=depth, location=loc)
    obj = bpy.context.active_object
    obj.name = name
    obj.rotation_euler = (0, R90, 0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    obj.data.materials.append(mat)
    add_bevel(obj, 0.02, 5)

    bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=radius * 0.7, depth=depth * 1.05, location=loc)
    flange = bpy.context.active_object
    flange.name = name + "_Flange"
    flange.rotation_euler = (0, R90, 0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    flange.data.materials.append(mat_chrome)
    add_bevel(flange, 0.01, 3)
    parent_keep_transform(flange, obj)
    return obj

def _part(o, mat, name):
    o.name = name
    if mat:
        o.data.materials.append(mat)
    return o

def P_box(size, loc, mat, rot=(0, 0, 0), name="part"):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.data.transform(Matrix.Diagonal((size[0], size[1], size[2], 1.0)))
    o.data.update()
    return _part(o, mat, name)

def P_cyl(r, depth, loc, mat, rot=(0, 0, 0), verts=32, name="part"):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
    return _part(bpy.context.active_object, mat, name)

def P_cone(r1, r2, depth, loc, mat, rot=(0, 0, 0), verts=32, name="part"):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth,
                                    location=loc, rotation=rot)
    return _part(bpy.context.active_object, mat, name)

def P_sphere(r, loc, mat, name="part"):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=r, location=loc)
    return _part(bpy.context.active_object, mat, name)

def P_torus(major, minor, loc, mat, rot=(0, 0, 0), name="part"):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor,
                                     major_segments=32, minor_segments=10,
                                     location=loc, rotation=rot)
    return _part(bpy.context.active_object, mat, name)

def group(parts, name, parent=None, shade=True):
    parts = [p for p in parts if p is not None]
    if not parts:
        return None
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    if shade:
        smooth(obj)
    if parent is not None:
        parent_keep_transform(obj, parent)
    return obj

def add_text(name, body, size, loc, rot, mat, parent=None, extrude=0.004):
    bpy.ops.object.text_add(location=loc, rotation=rot)
    t = bpy.context.active_object
    t.name = name
    t.data.body = body
    t.data.size = size
    t.data.align_x = 'CENTER'
    t.data.align_y = 'CENTER'
    t.data.extrude = extrude
    t.data.materials.append(mat)
    if parent is not None:
        parent_keep_transform(t, parent)
    return t

def boolean_cut(target, cutter, name):
    mod = target.modifiers.new(name="Cut_" + name, type='BOOLEAN')
    mod.object = cutter
    mod.operation = 'DIFFERENCE'
    mod.solver = 'FLOAT'
    cutter.display_type = 'WIRE'
    cutter.hide_render = True
    cutter.hide_viewport = True
    parent_keep_transform(cutter, target)

# =====================================================================
# 5. ROBOTIC ARM  (built UNSCALED at the origin, scaled + moved at the end)
# =====================================================================
bpy.ops.object.empty_add(type='PLAIN_AXES', location=(0, 0, 0))
carriage = bpy.context.active_object
carriage.name = "Arm_Carriage"
carriage.empty_display_size = 0.6

# ---- 5.1 base --------------------------------------------------------
base_lower = create_box("Base_Lower", (1.8, 1.8, 0.3), (0, 0, 0.15), mat_dark)
add_bevel(base_lower, 0.05, 6)
parent_keep_transform(base_lower, carriage)

base_upper = create_box("Base_Upper", (1.3, 1.3, 0.3), (0, 0, 0.45), mat_dark)
add_bevel(base_upper, 0.05, 6)
parent_keep_transform(base_upper, base_lower)

bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=0.35, depth=0.6, location=(0, 0, 0.9))
base_pillar = bpy.context.active_object
base_pillar.name = "Base_Pillar"
base_pillar.data.materials.append(mat_dark)
smooth(base_pillar)
parent_keep_transform(base_pillar, base_lower)

# ---- 5.2 links & joints (original geometry / pivots) -----------------
j1_loc = (0, 0, 1.15)
j1 = create_joint("Joint_1_Shoulder", 0.35, 1.0, j1_loc, mat_dark)
parent_keep_transform(j1, base_pillar)
arm_lower = create_box("Arm_Lower", (0.5, 0.45, 2.8), (0, 0, 2.55), mat_orange)
add_bevel(arm_lower, 0.05, 6)
set_pivot(arm_lower, j1_loc)
parent_keep_transform(arm_lower, j1)

j2_loc = (0, 0, 3.95)
j2 = create_joint("Joint_2_Elbow", 0.3, 0.9, j2_loc, mat_dark)
set_pivot(j2, j2_loc)
parent_keep_transform(j2, arm_lower)

arm_upper = create_box("Arm_Upper", (0.4, 0.35, 2.5), (0, 0, 5.2), mat_orange)
add_bevel(arm_upper, 0.05, 6)
set_pivot(arm_upper, j2_loc)
parent_keep_transform(arm_upper, j2)

j3_loc = (0, 0, 6.45)
j3 = create_joint("Joint_3_Wrist", 0.25, 0.8, j3_loc, mat_dark)
set_pivot(j3, j3_loc)
parent_keep_transform(j3, arm_upper)

effector_hub = create_box("Effector_Hub", (0.4, 0.35, 1.2), (0, 0, 7.05), mat_orange)
add_bevel(effector_hub, 0.05, 6)
set_pivot(effector_hub, j3_loc)
parent_keep_transform(effector_hub, j3)

j4_loc = (0, 0, 7.65)
j4 = create_joint("Joint_4_SensorPivot", 0.2, 0.6, j4_loc, mat_dark)
set_pivot(j4, j4_loc)
parent_keep_transform(j4, effector_hub)

# ---- 5.3 COMPACT SENSOR (was core 0.8 + needle 1.2; now 0.2 + 0.30) -----
TOOL_TIP_Z = 7.65 + TOOL_LEN_U
ring = P_cyl(0.1, 0.08, (0, 0, 7.89), mat_dark, verts=64, name="ring")
rings = group([ring], "J4_Sensor_Ring")
set_pivot(rings, j4_loc)
parent_keep_transform(rings, j4)

bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=0.06, depth=0.2, location=(0, 0, 7.95))
sensor_core = bpy.context.active_object
sensor_core.name = "Sensor_Core"
sensor_core.data.materials.append(mat_chrome)
smooth(sensor_core)
set_pivot(sensor_core, j4_loc)
parent_keep_transform(sensor_core, j4)

bpy.ops.mesh.primitive_cone_add(vertices=64, radius1=0.03, radius2=0.012,
                                depth=TOOL_TIP_Z - 7.95, location=(0, 0, (TOOL_TIP_Z + 7.95) / 2))
sensor_needle = bpy.context.active_object
sensor_needle.name = "Sensor_Needle"
sensor_needle.data.materials.append(mat_chrome)
smooth(sensor_needle)
set_pivot(sensor_needle, j4_loc)
parent_keep_transform(sensor_needle, sensor_core)

bpy.context.view_layer.update()

# empty exactly on the needle tip: used for verification, DOF and the close-up camera
bpy.ops.object.empty_add(type='SPHERE', location=(0, 0, TOOL_TIP_Z))
tip_obj = bpy.context.active_object
tip_obj.name = "Sensor_Tip"
tip_obj.empty_display_size = 0.02
parent_keep_transform(tip_obj, sensor_core)

# ---- 5.4 DETAIL: base ------------------------------------------------
def anchor_bolt(x, y, z0=0.3):
    return [
        P_cyl(0.11, 0.025, (x, y, z0 + 0.0125), mat_steel, verts=32, name="washer"),
        P_cyl(0.085, 0.09, (x, y, z0 + 0.07), mat_steel, rot=(0, 0, math.radians(15)), verts=6, name="nut"),
        P_cyl(0.04, 0.2, (x, y, z0 + 0.125), mat_steel, verts=16, name="stud"),
    ]

bolt_pts = [(sx * 0.78, sy * 0.78) for sx in (-1, 1) for sy in (-1, 1)]
bolt_pts += [(0.0, 0.78), (0.0, -0.78), (0.78, 0.0), (-0.78, 0.0)]
parts = []
for (bx, by) in bolt_pts:
    parts += anchor_bolt(bx, by)
group(parts, "Base_AnchorBolts", base_lower)

parts = []
for i in range(7):
    t = -0.78 + 0.26 * i
    parts.append(P_box((0.09, 0.012, 0.2), (t, -0.906, 0.15), mat_hazard, rot=(0, math.radians(35), 0), name="bar"))
    parts.append(P_box((0.09, 0.012, 0.2), (t, 0.906, 0.15), mat_hazard, rot=(0, math.radians(35), 0), name="bar"))
    parts.append(P_box((0.012, 0.09, 0.2), (-0.906, t, 0.15), mat_hazard, rot=(math.radians(35), 0, 0), name="bar"))
    parts.append(P_box((0.012, 0.09, 0.2), (0.906, t, 0.15), mat_hazard, rot=(math.radians(35), 0, 0), name="bar"))
group(parts, "Base_HazardStripes", base_lower)

parts = [P_cyl(0.5, 0.06, (0, 0, 0.63), mat_steel, verts=64, name="slew_flange"),
         P_cyl(0.4, 0.035, (0, 0, 0.675), mat_chrome, verts=64, name="slew_seal")]
for k in range(12):
    a = 2 * math.pi * k / 12
    parts.append(P_cyl(0.032, 0.04, (0.45 * math.cos(a), 0.45 * math.sin(a), 0.665),
                       mat_chrome, verts=16, name="cap_screw"))
group(parts, "Base_SlewRing", base_upper)

parts = [P_box((0.5, 0.14, 0.32), (0.0, -0.72, 0.45), mat_dark, name="jbox"),
         P_box((0.44, 0.02, 0.26), (0.0, -0.8, 0.45), mat_steel, name="jbox_lid")]
for sx in (-1, 1):
    for sz in (-1, 1):
        parts.append(P_cyl(0.015, 0.02, (sx * 0.2, -0.815, 0.45 + sz * 0.1), mat_chrome,
                           rot=AX_Y, verts=12, name="lid_screw"))
for gx in (-0.12, 0.12):
    parts.append(P_cyl(0.04, 0.09, (gx, -0.85, 0.42), mat_chrome, rot=AX_Y, verts=6, name="gland"))
    parts.append(P_cyl(0.022, 0.18, (gx, -0.87, 0.33), mat_rubber, verts=12, name="cable"))
group(parts, "Base_JunctionBox", base_upper)

parts = [P_box((0.14, 0.14, 0.18), (sx * 0.52, 0.0, 0.69), mat_hazard, name="hardstop") for sx in (-1, 1)]
group(parts, "Base_HardStops", base_upper)

# ---- 5.5 DETAIL: joints ------------------------------------------------
def joint_details(joint, loc, r, d, name, motor=True):
    m = SIDE
    x0, y0, z0 = loc
    parts = []
    for sx in (-1, 1):
        for k in range(10):
            a = 2 * math.pi * k / 10
            parts.append(P_cyl(r * 0.055, 0.035,
                               (x0 + sx * (d / 2 + 0.0175), y0 + r * 0.84 * math.cos(a), z0 + r * 0.84 * math.sin(a)),
                               mat_chrome, rot=AX_X, verts=6, name="bolt"))
    face = d * 0.525
    if motor:
        motor_len = 0.5 * d
        parts.append(P_cyl(r * 0.62, 0.06, (x0 + m * (face + 0.03), y0, z0), mat_steel, rot=AX_X, verts=48, name="adapter"))
        mx = x0 + m * (face + 0.06 + motor_len / 2)
        parts.append(P_cyl(r * 0.5, motor_len, (mx, y0, z0), mat_dark, rot=AX_X, verts=48, name="motor"))
        for i in range(6):
            fx = x0 + m * (face + 0.1 + i * (motor_len - 0.08) / 5)
            parts.append(P_cyl(r * 0.58, 0.018, (fx, y0, z0), mat_dark, rot=AX_X, verts=48, name="fin"))
        parts.append(P_cyl(r * 0.36, 0.1, (x0 + m * (face + 0.06 + motor_len + 0.05), y0, z0), mat_steel,
                           rot=AX_X, verts=32, name="encoder"))
        parts.append(P_cyl(r * 0.06, r * 0.22, (mx, y0, z0 + r * 0.55), mat_chrome, verts=12, name="connector"))
    else:   # compact wrist pivot: just a flat cap so it fits inside the cavity
        parts.append(P_cyl(r * 0.5, 0.05, (x0 + m * (face + 0.025), y0, z0), mat_steel, rot=AX_X, verts=48, name="cap"))
    parts.append(P_cyl(r * 0.5, 0.05, (x0 - m * (face + 0.025), y0, z0), mat_steel, rot=AX_X, verts=48, name="cover"))
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        parts.append(P_cyl(r * 0.035, 0.02, (x0 - m * (face + 0.06), y0 + r * 0.36 * math.cos(a), z0 + r * 0.36 * math.sin(a)),
                           mat_chrome, rot=AX_X, verts=6, name="cover_screw"))
    return group(parts, name + "_Details", joint)

joint_details(j1, j1_loc, 0.35, 1.0, "J1")
joint_details(j2, j2_loc, 0.30, 0.9, "J2")
joint_details(j3, j3_loc, 0.25, 0.8, "J3")
joint_details(j4, j4_loc, 0.20, 0.6, "J4", motor=False)

# ---- 5.6 DETAIL: links --------------------------------------------------
def link_details(link, name, x_half, y_half, z0, z1):
    L = z1 - z0
    sx_ = SIDE
    parts = []
    for f in (0.30, 0.66):
        parts.append(P_box((2 * x_half + 0.02, 2 * y_half + 0.02, 0.03), (0, 0, z0 + L * f), mat_orange, name="band"))
    for sy in (-1, 1):
        for (f, hf) in ((0.46, 0.20), (0.82, 0.10)):
            if sy == 1 and hf < 0.15:
                continue
            zc = z0 + L * f
            pw, ph = 2 * x_half * 0.7, L * hf * 2
            parts.append(P_box((pw, 0.016, ph), (0, sy * (y_half + 0.006), zc), mat_dark, name="panel"))
            for ax in (-1, 1):
                for az in (-1, 1):
                    parts.append(P_cyl(0.018, 0.016, (ax * (pw / 2 - 0.03), sy * (y_half + 0.02), zc + az * (ph / 2 - 0.03)),
                                       mat_chrome, rot=AX_Y, verts=6, name="screw"))
    for i in range(6):
        parts.append(P_box((0.012, 2 * y_half * 0.6, 0.022), (sx_ * (x_half + 0.002), 0, z0 + L * 0.12 + i * 0.045), mat_rubber, name="louvre"))
    zn = z0 + L * 0.62
    parts.append(P_box((0.014, y_half * 1.3, 0.1), (sx_ * (x_half + 0.004), 0, zn), mat_label, name="nameplate"))
    for sy in (-1, 1):
        for sz in (-1, 1):
            parts.append(P_cyl(0.007, 0.01, (sx_ * (x_half + 0.012), sy * y_half * 0.55, zn + sz * 0.035),
                               mat_chrome, rot=AX_X, verts=8, name="rivet"))
    cx, cy = sx_ * (x_half + 0.045), y_half * 0.55
    c0, c1 = z0 + 0.45, z1 - 0.4
    parts.append(P_cyl(0.03, c1 - c0, (cx, cy, (c0 + c1) / 2), mat_rubber, verts=16, name="conduit"))
    n_clamps = int((c1 - c0) / 0.5) + 1
    for i in range(n_clamps):
        zc = c0 + 0.12 + i * 0.5
        if zc > c1:
            break
        parts.append(P_cyl(0.047, 0.035, (cx, cy, zc), mat_chrome, verts=16, name="clamp"))
        parts.append(P_box((0.05, 0.04, 0.03), (sx_ * (x_half + 0.015), cy, zc), mat_chrome, name="clamp_foot"))
    return group(parts, name + "_Details", link)

link_details(arm_lower, "ArmLower", 0.25, 0.225, 1.15, 3.95)
link_details(arm_upper, "ArmUpper", 0.20, 0.175, 3.95, 6.45)

# lettering on the camera-facing (-X local) face, reading bottom-to-top
add_text("Label_ZANVAR", "ZANVAR", 0.2, (-0.203, 0, 5.35), (0, -R90, 0), mat_hazard, arm_upper)
add_text("Label_MHI01", "MHI-01", 0.09, (-0.203, 0, 4.35), (0, -R90, 0), mat_label, arm_upper)

# ---- 5.7 DETAIL: hub + compact probe ----------------------------------
parts = [P_box((0.012, 0.16, 0.05), (SIDE * 0.206, 0, 7.35), mat_led, name="status_led"),
         P_box((0.014, 0.2, 0.09), (SIDE * 0.204, 0, 7.35), mat_dark, name="led_housing"),
         P_cyl(0.035, 0.07, (SIDE * 0.235, 0.1, 6.85), mat_steel, rot=AX_X, verts=24, name="m12_connector"),
         P_cyl(0.045, 0.03, (SIDE * 0.205, 0.1, 6.85), mat_steel, rot=AX_X, verts=6, name="m12_nut"),
         P_cyl(0.012, 0.3, (SIDE * 0.26, 0.1, 6.68), mat_rubber, verts=10, name="m12_cable")]
for sy in (-1, 1):
    for sz in (-1, 1):
        parts.append(P_cyl(0.016, 0.012, (SIDE * 0.206, sy * 0.13, 6.75 + (sz + 1) * 0.1), mat_chrome, rot=AX_X, verts=6, name="screw"))
group(parts, "Hub_Details", effector_hub)

PX = -SIDE * 0.13       # proximity sensor offset (opposite the other details)
parts = [P_cyl(0.085, 0.04, (0, 0, 8.07), mat_chrome, verts=48, name="collar"),
         P_cyl(0.07, 0.015, (0, 0, 8.0975), mat_steel, verts=32, name="spring_seat_low"),
         P_cyl(0.07, 0.015, (0, 0, 8.2075), mat_steel, verts=32, name="spring_seat_high"),
         P_sphere(0.014, (0, 0, TOOL_TIP_Z - 0.014), mat_chrome, name="contact_ball"),
         P_box((0.12, 0.035, 0.035), (PX * 0.55, 0, 7.99), mat_dark, name="bracket"),
         P_cyl(0.026, 0.2, (PX, 0, 8.02), mat_blue, verts=32, name="prox_body"),
         P_cyl(0.029, 0.02, (PX, 0, 8.13), mat_steel, verts=32, name="prox_head"),
         P_cyl(0.028, 0.01, (PX, 0, 8.095), mat_led_cyan, verts=32, name="prox_led"),
         P_cyl(0.01, 0.18, (PX, 0, 7.83), mat_rubber, verts=10, name="prox_cable")]
for i in range(4):
    parts.append(P_torus(0.05, 0.008, (0, 0, 8.11 + 0.025 * i), mat_steel, name="spring_coil"))
group(parts, "Probe_Details", sensor_core)

bpy.context.view_layer.update()

# ---- 5.8 carriage body + base plate ---------------------------------------
carriage_body = create_box("Arm_Carriage_Body", (2.4, 2.4, 1.15), (0, 0, -0.575), mat_dark)
add_bevel(carriage_body, 0.04, 3)
parent_keep_transform(carriage_body, carriage)
bpy.context.view_layer.update()

parts = [P_box((2.1, 2.1, 0.05), (0, 0, -0.025), mat_steel, name="base_plate")]
plate_nuts = [(sx, sy) for sx in (-1.0, 1.0) for sy in (-1.0, 1.0)]
plate_nuts += [(0.0, -1.0), (0.0, 1.0), (-1.0, 0.0), (1.0, 0.0)]
for (px_, py_) in plate_nuts:
    parts.append(P_cyl(0.07, 0.05, (px_, py_, 0.025), mat_steel, rot=(0, 0, math.radians(15)), verts=6, name="plate_nut"))
parts.append(P_box((2.42, 2.42, 0.06), (0, 0, -0.35), mat_hazard, name="carriage_band"))
group(parts, "Arm_BasePlate", carriage_body)

# =====================================================================
# 6. RAIL, FLOOR MARKINGS
# =====================================================================
ys = [sm_center(i)[1] for i in range(5)]
RAIL_Y0, RAIL_Y1 = min(ys) - 2.6, max(ys) + 2.6
RAIL_MID, RAIL_LEN = (RAIL_Y0 + RAIL_Y1) / 2, RAIL_Y1 - RAIL_Y0
RAIL_GAUGE = 0.8 * K

parts = []
for sx in (-1, 1):
    parts.append(P_box((0.16, RAIL_LEN, RAIL_H - 0.04), (BASE_X + sx * RAIL_GAUGE, RAIL_MID, FLOOR_TOP + 0.04 + (RAIL_H - 0.04) / 2),
                       mat_steel, name="rail"))
n_sl = int(RAIL_LEN / 1.0) + 1
for i in range(n_sl):
    parts.append(P_box((2 * RAIL_GAUGE + 0.6, 0.3, 0.04), (BASE_X, RAIL_Y0 + 0.5 + i * 1.0, FLOOR_TOP + 0.02), mat_dark, name="sleeper"))
for ye in (RAIL_Y0 - 0.1, RAIL_Y1 + 0.1):
    parts.append(P_box((2 * RAIL_GAUGE + 0.6, 0.3, 0.4), (BASE_X, ye, FLOOR_TOP + 0.2), mat_hazard, name="end_stop"))
group(parts, "Arm_Rail", shade=False)

def floor_rect(cx, cy, hx, hy, w=0.08):
    z = FLOOR_TOP + 0.004
    return [P_box((2 * hx + w, w, 0.008), (cx, cy - hy, z), mat_hazard, name="line"),
            P_box((2 * hx + w, w, 0.008), (cx, cy + hy, z), mat_hazard, name="line"),
            P_box((w, 2 * hy + w, 0.008), (cx - hx, cy, z), mat_hazard, name="line"),
            P_box((w, 2 * hy + w, 0.008), (cx + hx, cy, z), mat_hazard, name="line")]

group(floor_rect(BASE_X, RAIL_MID, RAIL_GAUGE + 1.0, RAIL_LEN / 2 + 0.7) +
      floor_rect(0, frame_loc[1], 4.7, 5.5), "Floor_SafetyMarkings", shade=False)

# =====================================================================
# 7. MOLD ASSEMBLY (your geometry + detailing)
# =====================================================================
outer_frame = create_box("Outer_Flask_Frame", (9.0 * S, 11.0 * S, 2.2 * S), frame_loc, mat_iron)
add_bevel(outer_frame, 0.1 * S, 4, shade=False)

for x in [-4.6 * S, 4.6 * S]:
    for y in [-2.0 * S, 2.0 * S]:
        bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=0.4 * S, depth=1.5 * S,
                                            location=(x, frame_loc[1] + y, frame_loc[2]))
        lug = bpy.context.active_object
        lug.name = "Flask_Lug"
        lug.data.materials.append(mat_iron)
        add_bevel(lug, 0.05 * S, 3)
        parent_keep_transform(lug, outer_frame)

frame_hollow = create_box("Frame_Hollow", (8.2 * S, 10.2 * S, 3.0 * S), frame_loc, None)
boolean_cut(outer_frame, frame_hollow, "FrameHollow")

main_mold = create_box("Main_Sand_Mold", (8.0 * S, 10.0 * S, 2.0 * S), sand_loc, mat_sand)
add_bevel(main_mold, 0.08 * S, 4, shade=False)

def generate_scaled_cavity(name, loc_x, relative_y, color_mat):
    abs_x = loc_x * S
    abs_y = (relative_y * S) + sand_loc[1]
    z_base = sand_loc[2] + (0.8 * S)
    z_step = sand_loc[2] + (0.3 * S)
    base = create_box(f"{name}_Base", (2.0 * S, 2.0 * S, 1.2 * S), (abs_x, abs_y, z_base), None)
    step = create_box(f"{name}_Step", (1.2 * S, 1.2 * S, 1.0 * S), (abs_x, abs_y, z_step), None)
    cavity = join_and_color(base, [step], color_mat)
    cavity.name = name
    boolean_cut(main_mold, cavity, name)

for (nm, rx, ry), col in zip(SUBMOLDS, (mat_hazard, mat_blue, mat_ok, mat_hazard, mat_blue)):
    generate_scaled_cavity(nm, rx, ry, col)

def mold_xy(rx, ry):
    return (rx * S, ry * S + sand_loc[1])

gate_parts = []
for ry in (2.6, -2.6):
    gx, gy = mold_xy(0, ry)
    gate_parts.append(P_box((2.6 * S, 0.3 * S, 0.8 * S), (gx, gy, sand_top), None, name="runner"))
    gate_parts.append(P_cone(0.25 * S, 0.6 * S, 1.0 * S, (gx, gy, sand_top - 0.1 * S), None, verts=48, name="sprue_cup"))
for ry in (1.8, -1.8):
    gx, gy = mold_xy(0, ry)
    gate_parts.append(P_box((0.3 * S, 1.6 * S, 0.8 * S), (gx, gy, sand_top), None, name="runner"))
gating = group(gate_parts, "Gating_Cutter", shade=False)
boolean_cut(main_mold, gating, "Gating")

vx, vy1 = mold_xy(-3.5, 1.3)
_, vy2 = mold_xy(-3.5, -1.3)
vent_parts = [P_cyl(0.07 * S, 1.2 * S, (vx, vy1, sand_top - 0.3 * S), None, verts=12, name="vent"),
              P_cyl(0.07 * S, 1.2 * S, (vx, vy2, sand_top - 0.3 * S), None, verts=12, name="vent")]
vents = group(vent_parts, "Vent_Cutter", shade=False)
arr = vents.modifiers.new(name="VentArray", type='ARRAY')
arr.count = 8
arr.use_relative_offset = False
arr.use_constant_offset = True
arr.constant_offset_displace = (1.0 * S, 0.0, 0.0)
boolean_cut(main_mold, vents, "Vents")

chalk = [("C1", -2.2, 4.3), ("C2", 2.2, 4.3), ("C3", 3.0, 0.0), ("C4", -2.2, -4.3), ("C5", 2.2, -4.3)]
for label, rx, ry in chalk:
    lx, ly = mold_xy(rx, ry)
    add_text("Chalk_" + label, label, 0.55 * S, (lx, ly, sand_top + 0.004), (0, 0, 0), mat_chalk, main_mold, extrude=0.002)

parts = []
for sx in (-1, 1):
    for sy in (-1, 1):
        px = sx * 4.3 * S
        py = frame_loc[1] + sy * 5.3 * S
        parts.append(P_cyl(0.14 * S, 0.7 * S, (px, py, frame_top + 0.25 * S), mat_steel, verts=32, name="guide_pin"))
        parts.append(P_cone(0.14 * S, 0.05 * S, 0.25 * S, (px, py, frame_top + 0.725 * S), mat_steel, verts=32, name="pin_tip"))
group(parts, "Flask_GuidePins", outer_frame)

parts = []
for sx in (-1, 1):
    for ry in (-3.5, 0.0, 3.5):
        cx_ = sx * (4.5 * S + 0.22 * S)
        cy_ = frame_loc[1] + ry * S
        parts.append(P_box((0.44 * S, 1.0 * S, 1.5 * S), (cx_, cy_, frame_top - 0.35 * S), mat_dark, name="clamp_body"))
        parts.append(P_cyl(0.13 * S, 0.9 * S, (sx * (4.5 * S + 0.55 * S), cy_, frame_top - 0.2 * S), mat_steel, rot=AX_X, verts=24, name="clamp_screw"))
        parts.append(P_cyl(0.2 * S, 0.15 * S, (sx * (4.5 * S + 1.05 * S), cy_, frame_top - 0.2 * S), mat_steel, rot=AX_X, verts=6, name="clamp_head"))
group(parts, "Flask_Clamps", outer_frame)

parts = []
for sy in (-1, 1):
    parts.append(P_torus(0.7 * S, 0.13 * S, (0, frame_loc[1] + sy * (5.5 * S + 0.12 * S), frame_loc[2]),
                         mat_iron, rot=(R90, 0, 0), name="handle"))
group(parts, "Flask_Handles", outer_frame)

add_text("Flask_Stencil_Front", "FLASK 01  -  ZANVAR FOUNDRY", 0.42 * S,
         (0, frame_loc[1] - 5.5 * S - 0.003, frame_loc[2]), (R90, 0, 0), mat_chalk, outer_frame, extrude=0.003)
add_text("Flask_Stencil_Back", "FLASK 01  -  ZANVAR FOUNDRY", 0.42 * S,
         (0, frame_loc[1] + 5.5 * S + 0.003, frame_loc[2]), (R90, 0, math.pi), mat_chalk, outer_frame, extrude=0.003)

skid_z = frame_loc[2] - 1.1 * S
cy0 = frame_loc[1]
parts = [P_box((10.0 * S, 12.0 * S, 0.12), (0, cy0, skid_z - 0.06), mat_dark, name="skid_plate")]
for sx in (-1, 1):
    parts.append(P_box((10.0 * S, 0.3, 0.18), (0, cy0 + sx * 2.6, skid_z - 0.21), mat_dark, name="skid_beam"))
leg_h = (skid_z - 0.12) - FLOOR_TOP
for sx in (-1, 1):
    for sy in (-1, 1):
        parts.append(P_box((0.3, 0.3, leg_h), (sx * (5.0 * S - 0.4), cy0 + sy * (6.0 * S - 0.4), FLOOR_TOP + leg_h / 2), mat_dark, name="skid_leg"))
        parts.append(P_box((0.45, 0.45, 0.04), (sx * (5.0 * S - 0.4), cy0 + sy * (6.0 * S - 0.4), FLOOR_TOP + 0.02), mat_steel, name="foot_plate"))
for sx in (-1, 1):
    parts.append(P_box((10.0 * S, 0.05, 0.02), (0, cy0 + sx * (6.0 * S - 0.03), skid_z + 0.002), mat_hazard, name="skid_edge"))
group(parts, "Mold_SupportSkid")

smooth(outer_frame)
smooth(main_mold)

floor = create_box("Studio_Floor", (50.0, 50.0, 0.1), (0, 0, -1.2), mat_floor)

# =====================================================================
# 8. KINEMATICS  (pure maths - no Blender calls)
# =====================================================================
# Angle convention: phi = direction of a link measured from vertical-UP toward the
# forward (+X) direction.  Unposed arm: every link points straight up (phi = 0).
#   direction vector in the arm plane = (sin phi, cos phi)
# Joint rotation about local X:  a1 = phi1,  a2 = phi2-phi1,  a3 = phi3-phi2,  a4 = phi4-phi3
# phi3 = hub direction (fixed lean), phi4 = tool direction:
#   180 deg = needle points DOWN (vertical probe), 90 = +X wall, 270 = -X wall

def solve_ik(px, pz, phi4_deg, lean_deg=None):
    """px,pz = world X/Z of the J4 pivot.  Returns (a1,a2,a3,a4) in radians."""
    if lean_deg is None:
        lean_deg = HUB_LEAN_DEG
    rho = px - BASE_X
    wz = pz - Z_J1
    lean = math.radians(lean_deg)
    r3 = rho - L3 * math.sin(lean)             # J3 position (hub points forward-down by 'lean')
    z3 = wz + L3 * math.cos(lean)
    d = math.hypot(r3, z3)
    if d > L1 + L2 - 0.02 or d < abs(L1 - L2) + 0.02:
        raise ValueError("UNREACHABLE: pivot x=%.2f z=%.2f needs reach %.2f (allowed %.2f..%.2f)"
                         % (px, pz, d, abs(L1 - L2) + 0.02, L1 + L2 - 0.02))
    psi = math.atan2(r3, z3)
    g1 = math.acos((L1 * L1 + d * d - L2 * L2) / (2 * L1 * d))
    g2 = math.acos((L2 * L2 + d * d - L1 * L1) / (2 * L2 * d))
    phi1 = psi - g1                             # elbow UP solution
    phi2 = psi + g2
    phi3 = math.pi - lean
    phi4 = math.radians(phi4_deg)
    return (phi1, phi2 - phi1, phi3 - phi2, phi4 - phi3)

# =====================================================================
# 9. SIMULATED READINGS
# =====================================================================
PTS = ('V', 'H1', 'H2')
rng = random.Random(SEED)
span = HARD_MAX - HARD_MIN
READINGS = {}
for _i in range(5):
    for _p in PTS:
        READINGS[(_i, _p)] = round(rng.uniform(HARD_MIN + 0.25 * span, HARD_MAX - 0.25 * span), 1)
if SIMULATE_FAILURE:
    READINGS[FAIL_AT] = round(HARD_MIN - 0.3 * span, 1)

def in_limits(v):
    return HARD_MIN <= v <= HARD_MAX

# =====================================================================
# 10. MOTION PLAN -> TIMELINE
# =====================================================================
class Timeline:
    def __init__(self):
        self.samples = []      # (frame, y, angles)
        self.events = []       # (f0, f1, label, sm, pt)
        self.contacts = []     # (frame, sm, pt, (x, y, z), expected tool axis)
        self.read_frame = {}   # (sm, pt) -> frame the reading appears
        self.frame = 1
        self.cur = None

    def _emit(self, task):
        self.samples.append((self.frame, task['y'], solve_ik(task['px'], task['pz'], task['phi'], task.get('lean'))))

    def start(self, task):
        self.cur = dict(task)
        self._emit(task)

    def move(self, task, speed, label, sm=None, pt=None, min_dur=0.5, rot_speed=70.0):
        a = self.cur
        dist = math.sqrt((task['y'] - a['y']) ** 2 + (task['px'] - a['px']) ** 2 + (task['pz'] - a['pz']) ** 2)
        dur = max(dist / speed, abs(task['phi'] - a['phi']) / rot_speed, min_dur) / SPEED
        n = max(1, int(round(dur * FPS)))
        f0 = self.frame
        for k in range(1, n + 1):
            s = k / n
            e = s * s * (3 - 2 * s)
            t = {key: a[key] + (task[key] - a[key]) * e for key in a}
            self.frame += 1
            self._emit(t)
        self.cur = dict(task)
        self.events.append((f0, self.frame, label, sm, pt))

    def hold(self, dur, label, sm=None, pt=None):
        n = max(1, int(round(dur / SPEED * FPS)))
        f0 = self.frame
        for _ in range(n):
            self.frame += 1
            self._emit(self.cur)
        self.events.append((f0, self.frame, label, sm, pt))

V_FAST, V_SLOW, V_TRAVEL = 1.8, 0.28, 3.2

def build_plan():
    tl = Timeline()
    cx0, cy0_ = sm_center(0)
    home = dict(y=cy0_, px=BASE_X + 3.2, pz=4.6, phi=180.0, lean=HUB_LEAN_DEG)
    tl.start(home)
    tl.hold(0.8, "HOME")
    for i in range(5):
        cx, cy = sm_center(i)
        above = dict(y=cy, px=cx, pz=TRAVEL_PZ, phi=180.0, lean=HUB_LEAN_DEG)
        tl.move(above, V_TRAVEL, "MOVING", i, None, min_dur=1.2)

        for pt in PTS:
            if pt == 'V':
                tip = (cx, cy, Z_PIT)
                contact = dict(y=cy, px=cx, pz=Z_PIT + LT, phi=180.0, lean=HUB_LEAN_DEG)
                stand = dict(contact, pz=contact['pz'] + STANDOFF_V)
                hover = above
                axis = (0, 0, -1)
            elif pt == 'H1':                         # left wall, needle points -X
                tip = (cx - WALL_HALF, cy, Z_WALL)
                contact = dict(y=cy, px=cx - WALL_HALF + LT, pz=Z_WALL, phi=270.0, lean=HUB_LEAN_DEG)
                stand = dict(contact, px=contact['px'] + STANDOFF_H)
                hover = dict(stand, pz=TRAVEL_PZ)
                axis = (-1, 0, 0)
            else:                                    # right wall, needle points +X
                tip = (cx + WALL_HALF, cy, Z_WALL)
                contact = dict(y=cy, px=cx + WALL_HALF - LT, pz=Z_WALL, phi=90.0, lean=H2_LEAN_DEG)
                stand = dict(contact, px=contact['px'] - STANDOFF_H)
                hover = dict(stand, pz=TRAVEL_PZ)
                axis = (1, 0, 0)

            if pt != 'V':
                tl.move(hover, V_TRAVEL, "ALIGNING", i, pt, min_dur=1.0)
            tl.move(stand, V_FAST, "DESCENDING", i, pt)
            tl.move(contact, V_SLOW, "APPROACHING", i, pt, min_dur=0.5)
            tl.contacts.append((tl.frame, i, pt, tip, axis))
            tl.hold(DWELL_S, "MEASURING", i, pt)
            tl.read_frame[(i, pt)] = tl.frame
            tl.move(stand, V_SLOW, "RETRACTING", i, pt, min_dur=0.5)
            tl.move(hover, V_FAST, "ASCENDING", i, pt)

    tl.move(home, V_TRAVEL, "RETURNING_HOME", None, None, min_dur=2.0)
    tl.hold(1.0, "DONE")
    return tl

PLAN = build_plan()

# =====================================================================
# 11. APPLY THE PLAN: carriage + joints become keyframed animation
# =====================================================================
carriage.scale = (K, K, K)
carriage.rotation_euler = (0, 0, R90)
carriage.location = (BASE_X, PLAN.samples[0][1], Z_C)
bpy.context.view_layer.update()

_prefs_edit = bpy.context.preferences.edit
_old_interp = None
try:
    _old_interp = _prefs_edit.keyframe_new_interpolation_type
    _prefs_edit.keyframe_new_interpolation_type = 'LINEAR'
except Exception:
    pass

def key_channels(channels, samples_vals, frames):
    """channels: [(obj, data_path, index)]; samples_vals[n][c]; only real changes are keyed."""
    last = len(frames) - 1
    for c, (obj, path, idx) in enumerate(channels):
        for n, f in enumerate(frames):
            v = samples_vals[n][c]
            changed = (n == 0 or n == last or abs(v - samples_vals[n - 1][c]) > 1e-9
                       or abs(v - samples_vals[n + 1][c]) > 1e-9)
            if changed:
                getattr(obj, path)[idx] = v
                obj.keyframe_insert(data_path=path, index=idx, frame=f)

_channels = [(carriage, 'location', 1), (j1, 'rotation_euler', 0), (j2, 'rotation_euler', 0),
             (j3, 'rotation_euler', 0), (j4, 'rotation_euler', 0)]
_frames = [s[0] for s in PLAN.samples]
_vals = [(s[1], s[2][0], s[2][1], s[2][2], s[2][3]) for s in PLAN.samples]
key_channels(_channels, _vals, _frames)

# ---- inspection point markers (orange = pending, green/red = result) ----------
bpy.ops.object.empty_add(type='PLAIN_AXES', location=(0, 0, 0))
points_root = bpy.context.active_object
points_root.name = "Inspection_Points"
points_root.empty_display_size = 0.3

def make_marker_pair(i, pt, tip, axis, frame):
    x, y, z = tip
    off = 0.004
    if axis == (0, 0, -1):
        loc = (x, y, z + off); rot = (0, 0, 0)
    elif axis == (-1, 0, 0):
        loc = (x + off, y, z); rot = AX_X
    else:
        loc = (x - off, y, z); rot = AX_X
    pend = P_cyl(0.045, 0.012, loc, mat_pending, rot=rot, verts=24, name=f"SM{i+1}_{pt}_pending")
    ok = in_limits(READINGS[(i, pt)])
    res = P_cyl(0.10, 0.016, loc, mat_ok if ok else mat_bad, rot=rot, verts=32, name=f"SM{i+1}_{pt}_result")
    for o in (pend, res):
        parent_keep_transform(o, points_root)
    pend.scale = (1, 1, 1); pend.keyframe_insert(data_path="scale", frame=1)
    pend.keyframe_insert(data_path="scale", frame=max(1, frame - 1))
    pend.scale = (0, 0, 0); pend.keyframe_insert(data_path="scale", frame=frame)
    res.scale = (0, 0, 0); res.keyframe_insert(data_path="scale", frame=1)
    res.keyframe_insert(data_path="scale", frame=max(1, frame - 1))
    res.scale = (1, 1, 1); res.keyframe_insert(data_path="scale", frame=frame)

for (f, i, pt, tip, axis) in PLAN.contacts:
    make_marker_pair(i, pt, tip, axis, PLAN.read_frame[(i, pt)])

# ---- cameras ------------------------------------------------------------------
bpy.ops.object.empty_add(type='PLAIN_AXES', location=(-1.5, -6.07, 3.2))
view_target = bpy.context.active_object
view_target.name = "View_Target"

bpy.ops.object.camera_add(location=(4, -31, 11))
cam = bpy.context.active_object
cam.name = "Cam_Main"
cam.data.lens = 40
con = cam.constraints.new('TRACK_TO')
con.target = view_target
con.track_axis = 'TRACK_NEGATIVE_Z'
con.up_axis = 'UP_Y'
cam.data.dof.use_dof = True
cam.data.dof.focus_object = view_target
cam.data.dof.aperture_fstop = 11.0
bpy.context.scene.camera = cam

bpy.ops.object.camera_add(location=(0, -10, 5))
cam2 = bpy.context.active_object
cam2.name = "Cam_Probe_Closeup"
cam2.data.lens = 50
con2 = cam2.constraints.new('TRACK_TO')
con2.target = tip_obj
con2.track_axis = 'TRACK_NEGATIVE_Z'
con2.up_axis = 'UP_Y'
cam2.data.dof.use_dof = True
cam2.data.dof.focus_object = tip_obj
cam2.data.dof.aperture_fstop = 6.0

def closeup_loc(i):
    cx, cy = sm_center(i)
    return (cx, cy - 4.5, 4.5)

first_f = {}
last_f = {}
for (f0, f1, label, sm, pt) in PLAN.events:
    if sm is not None:
        first_f.setdefault(sm, f0)
        last_f[sm] = f1
cam2.location = closeup_loc(0)
cam2.keyframe_insert(data_path="location", frame=1)
for i in range(5):
    cam2.location = closeup_loc(i)
    cam2.keyframe_insert(data_path="location", frame=first_f[i])
    cam2.keyframe_insert(data_path="location", frame=last_f[i])
cam2.location = closeup_loc(4)
cam2.keyframe_insert(data_path="location", frame=PLAN.frame)

if _old_interp is not None:
    try:
        _prefs_edit.keyframe_new_interpolation_type = _old_interp
    except Exception:
        pass

# ---- lights --------------------------------------------------------------------
def aim(obj, target):
    c = obj.constraints.new('TRACK_TO')
    c.target = target
    c.track_axis = 'TRACK_NEGATIVE_Z'
    c.up_axis = 'UP_Y'

bpy.ops.object.light_add(type='SPOT', location=(8, -24, 20))
key_light = bpy.context.active_object
key_light.name = "Key_Light"
key_light.data.energy = 60000.0
key_light.data.color = (1.0, 0.95, 0.9)
key_light.data.spot_size = math.radians(70)
key_light.data.spot_blend = 0.8
aim(key_light, view_target)

bpy.ops.object.light_add(type='AREA', location=(-20, -14, 13))
fill = bpy.context.active_object
fill.name = "Fill_Light"
fill.data.energy = 14000.0
fill.data.size = 20.0
fill.data.color = (0.7, 0.85, 1.0)
aim(fill, view_target)

bpy.ops.object.light_add(type='AREA', location=(-8, 6, 11))
rim = bpy.context.active_object
rim.name = "Rim_Light"
rim.data.energy = 9000.0
rim.data.size = 12.0
rim.data.color = (1.0, 0.85, 0.7)
aim(rim, view_target)

# =====================================================================
# 12. LIVE STATUS BOARD (updates while the timeline plays)
# =====================================================================
BOARD_X, BOARD_Y = 0.5, -0.4
board_parts = [P_box((8.4, 0.18, 3.8), (BOARD_X, BOARD_Y, 4.3), mat_dark, name="board"),
               P_box((8.0, 0.02, 3.4), (BOARD_X, BOARD_Y - 0.1, 4.3), mat_screen, name="screen")]
for sx in (-1, 1):
    board_parts.append(P_box((0.25, 0.25, 4.3 - 0.4 - FLOOR_TOP), (BOARD_X + sx * 3.6, BOARD_Y + 0.1, (2.4 + FLOOR_TOP) / 2), mat_dark, name="post"))
group(board_parts, "Status_Board")

LINE_Z = [5.65, 5.2, 4.7, 4.3, 3.9, 3.5, 3.1, 2.7]
LINE_SIZE = [0.36, 0.30, 0.27, 0.27, 0.27, 0.27, 0.27, 0.30]
DISP_NAMES = ["Disp_%d" % n for n in range(8)]
for n in range(8):
    add_text(DISP_NAMES[n], "...", LINE_SIZE[n], (BOARD_X, BOARD_Y - 0.115, LINE_Z[n]), (R90, 0, 0), mat_text, extrude=0.002)

PT_LABEL = {'V': 'V - TOP (VERTICAL)', 'H1': 'H1 - LEFT WALL (HORIZ.)', 'H2': 'H2 - RIGHT WALL (HORIZ.)'}
_last_text = {}

def _val(i, p, f):
    return "%.1f" % READINGS[(i, p)] if f >= PLAN.read_frame[(i, p)] else "--.-"

def zanvar_frame_handler(scn, *args):
    f = scn.frame_current
    status = "READY"
    for (f0, f1, label, sm, pt) in PLAN.events:
        if f0 <= f <= f1:
            status = label if sm is None else "SM%d  %s  -  %s" % (sm + 1, PT_LABEL.get(pt, "TRAVEL"), label)
    lines = ["ZANVAR MOLD HARDNESS INSPECTION  (SIMULATION)", status]
    all_done, any_fail = True, False
    for i in range(5):
        done = all(f >= PLAN.read_frame[(i, p)] for p in PTS)
        verdict = "..."
        if done:
            ok = all(in_limits(READINGS[(i, p)]) for p in PTS)
            verdict = "PASS" if ok else "FAIL"
            any_fail = any_fail or not ok
        else:
            all_done = False
        lines.append("SM%d   V %s   H1 %s   H2 %s   %s" % (i + 1, _val(i, 'V', f), _val(i, 'H1', f), _val(i, 'H2', f), verdict))
    overall = ("FAIL" if any_fail else "PASS") if all_done else "IN PROGRESS"
    lines.append("OVERALL: %s   |   LIMITS %.0f-%.0f   |   ALL VALUES SIMULATED" % (overall, HARD_MIN, HARD_MAX))
    for name, txt in zip(DISP_NAMES, lines):
        if _last_text.get(name) != txt:
            ob = bpy.data.objects.get(name)
            if ob is not None:
                ob.data.body = txt
            _last_text[name] = txt

_remove_old_handlers()
bpy.app.handlers.frame_change_post.append(zanvar_frame_handler)

# =====================================================================
# 13. TIMELINE, VIEWPORT, SELF-CHECK
# =====================================================================
scene.frame_start = 1
scene.frame_end = PLAN.frame
scene.frame_set(1)
zanvar_frame_handler(scene)

for area in bpy.context.screen.areas:
    if area.type == 'VIEW_3D':
        for space in area.spaces:
            if space.type == 'VIEW_3D':
                space.shading.type = 'RENDERED'

def verify_contacts():
    """Drive the timeline to every contact frame and measure the REAL needle-tip error."""
    worst = 0.0
    print("-" * 70)
    print("ZANVAR contact verification  (tip error in mm, needle axis error in deg)")
    try:
        for (f, i, pt, tip, axis) in PLAN.contacts:
            scene.frame_set(f)
            bpy.context.view_layer.update()
            tp = tip_obj.matrix_world.translation
            err = (tp - Vector(tip)).length * 1000.0
            ax = sensor_core.matrix_world.to_quaternion() @ Vector((0, 0, 1))
            ang = math.degrees(ax.angle(Vector(axis)))
            worst = max(worst, err)
            flag = "" if err < 2.0 and ang < 0.5 else "   <-- CHECK"
            print("  SM%d %-2s  frame %4d  tip error %6.3f mm   axis error %5.2f deg%s" % (i + 1, pt, f, err, ang, flag))
        print("  worst tip error: %.3f mm" % worst)
    except Exception as exc:
        print("  (verification skipped: %s)" % exc)
    print("-" * 70)
    scene.frame_set(1)

verify_contacts()

print("[ZANVAR] Simulation built: %d frames (%.0f s at %d fps).  Press SPACE to play." %
      (PLAN.frame, PLAN.frame / FPS, FPS))
print("[ZANVAR] Cameras: Cam_Main (active) and Cam_Probe_Closeup - select it and press Ctrl+Numpad0 to view.")
if AUTOPLAY:
    try:
        bpy.ops.screen.animation_play()
    except Exception:
        pass
