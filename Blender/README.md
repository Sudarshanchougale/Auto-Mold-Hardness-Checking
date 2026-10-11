# ZANVAR: Automated Mold Hardness Inspection Simulation

A fully procedural, Python-driven 3D simulation for Blender. This project generates a photorealistic industrial environment where a rail-mounted robotic arm performs precise hardness checks on a multi-cavity foundry sand mold. 

Everything—from the geometry and kinematics to the procedural materials and studio lighting—is generated automatically via a single Python script.

## 🌟 Key Features

* **Custom Inverse Kinematics (IK) Engine:** Calculates exact joint angles (yaw, shoulder, elbow, wrist, and sensor pivot) to ensure mathematically perfect tip-to-surface contact for every inspection point.
* **Multi-Point Inspection Routine:** The arm automatically inspects 5 sub-molds, checking 3 specific points per cavity (1 vertical floor check, 2 horizontal wall checks).
* **Rail-Mounted Carriage:** The robotic arm is mounted on a sliding track along the X-axis, allowing it to dynamically position itself and reach all distant mold cavities.
* **Live Status Board:** A 3D digital display board tracks the robot's state (e.g., APPROACHING, MEASURING) and displays simulated hardness readings in real-time as the animation plays.
* **Self-Verifying Math:** Upon execution, the script verifies its own IK solver by calculating the exact tip-to-target physical error and prints the variance (in millimeters) directly to the Blender system console.
* **Procedural Texturing:** Uses 100% node-based procedural materials (powder-coated metal, granular foundry sand, brushed steel, cast iron) requiring no external image assets.
* **Cinematic Camera Setup:** Automatically generates `Cam_Main` for a wide overview and `Cam_Probe_Closeup` that tracks the sensor tip with precise Depth of Field (DoF).

## 🛠️ Requirements
* **Software:** Blender 3.x, 4.x, or 5.x (Fully compatible with Eevee and Eevee Next rendering engines).
* **Dependencies:** None. Standard Blender Python (`bpy`, `math`, `mathutils`) is used exclusively.

## 🚀 How to Run the Simulation

1. Open a new or empty project in Blender.
2. Navigate to the **Scripting** workspace at the top of the screen.
3. Click **New** (or Open) in the Text Editor and paste the contents of `zanvar_simulation.py`.
4. Run the script by clicking the ▶️ play icon in the text editor or pressing `Alt + P`.
5. Hover your mouse over the 3D viewport and press `SPACEBAR` to play the animation.

*Note: The script safely clears the scene upon execution. You can tweak the code and re-run the script as many times as you like without duplicating geometry.*

## ⚙️ Configuration & Customization

You can easily adjust the simulation parameters by modifying the `USER SETTINGS` block at the top of the Python script[cite: 6]:

| Variable | Description |
| :--- | :--- |
| `ARM_SCALE` | Adjusts the uniform scale of the robotic arm to accommodate different mold dimensions. |
| `SPEED` | Controls the animation playback speed multiplier (`1.0` is normal, `2.0` is twice as fast). |
| `HARD_MIN` / `HARD_MAX` | Defines the acceptable passing hardness threshold (default is 75.0 to 95.0). |
| `SIMULATE_FAILURE` | Set to `True` to force a specific inspection point to fail, which dynamically updates the digital status board and marker colors to red. |

## 🎥 Viewing Tips
To view the automated close-up camera during playback:
1. Select `Cam_Probe_Closeup` in the Outliner (top right).
2. Press `Ctrl + Numpad 0` to snap your viewport to the active camera.
