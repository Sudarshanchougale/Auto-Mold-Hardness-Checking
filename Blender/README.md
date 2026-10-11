# ZANVAR: Automated Mold Hardness Inspection Simulation

A fully procedural, Python-driven 3D simulation for Blender[cite: 6]. This project generates a photorealistic industrial environment where a rail-mounted robotic arm performs precise hardness checks on a multi-cavity foundry sand mold[cite: 6]. 

Everything—from the geometry and kinematics to the procedural materials and studio lighting—is generated automatically via a single Python script[cite: 6].

## 🌟 Key Features

* **Custom Inverse Kinematics (IK) Engine:** Calculates exact joint angles (yaw, shoulder, elbow, wrist, and sensor pivot) to ensure mathematically perfect tip-to-surface contact for every inspection point[cite: 6].
* **Multi-Point Inspection Routine:** The arm automatically inspects 5 sub-molds, checking 3 specific points per cavity (1 vertical floor check, 2 horizontal wall checks)[cite: 6].
* **Rail-Mounted Carriage:** The robotic arm is mounted on a sliding track along the X-axis, allowing it to dynamically position itself and reach all distant mold cavities[cite: 6].
* **Live Status Board:** A 3D digital display board tracks the robot's state (e.g., APPROACHING, MEASURING) and displays simulated hardness readings in real-time as the animation plays[cite: 6].
* **Self-Verifying Math:** Upon execution, the script verifies its own IK solver by calculating the exact tip-to-target physical error and prints the variance (in millimeters) directly to the Blender system console[cite: 6].
* **Procedural Texturing:** Uses 100% node-based procedural materials (powder-coated metal, granular foundry sand, brushed steel, cast iron) requiring no external image assets[cite: 6].
* **Cinematic Camera Setup:** Automatically generates `Cam_Main` for a wide overview and `Cam_Probe_Closeup` that tracks the sensor tip with precise Depth of Field (DoF)[cite: 6].

## 🛠️ Requirements
* **Software:** Blender 3.x, 4.x, or 5.x (Fully compatible with Eevee and Eevee Next rendering engines)[cite: 6].
* **Dependencies:** None. Standard Blender Python (`bpy`, `math`, `mathutils`) is used exclusively[cite: 6].

## 🚀 How to Run the Simulation

1. Open a new or empty project in Blender.
2. Navigate to the **Scripting** workspace at the top of the screen[cite: 6].
3. Click **New** (or Open) in the Text Editor and paste the contents of `Robotic_Arm.py`[cite: 6].
4. Run the script by clicking the ▶️ play icon in the text editor or pressing `Alt + P`[cite: 6].
5. Hover your mouse over the 3D viewport and press `SPACEBAR` to play the animation[cite: 6].

*Note: The script safely clears the scene upon execution. You can tweak the code and re-run the script as many times as you like without duplicating geometry[cite: 6].*

## ⚙️ Configuration & Customization

You can easily adjust the simulation parameters by modifying the `USER SETTINGS` block at the top of the Python script[cite: 6]:

| Variable | Description |
| :--- | :--- |
| `ARM_SCALE` | Adjusts the uniform scale of the robotic arm to accommodate different mold dimensions[cite: 6]. |
| `SPEED` | Controls the animation playback speed multiplier (`1.0` is normal, `2.0` is twice as fast)[cite: 6]. |
| `HARD_MIN` / `HARD_MAX` | Defines the acceptable passing hardness threshold (default is 75.0 to 95.0)[cite: 6]. |
| `SIMULATE_FAILURE` | Set to `True` to force a specific inspection point to fail, which dynamically updates the digital status board and marker colors to red[cite: 6]. |

## 🎥 Viewing Tips
To view the automated close-up camera during playback:
1. Select `Cam_Probe_Closeup` in the Outliner (top right).
2. Press `Ctrl + Numpad 0` to snap your viewport to the active camera.
