import sys
import time
import sender
from reciever import ObservationReceiver
import threading
import cv2
import numpy as np
import os

# --- Configuration ---
NUM_CARS = 1
OUTPUT_DIR = "cameraTest"  # folder to save images
os.makedirs(OUTPUT_DIR, exist_ok=True)  # create it if it doesn't exist

if __name__ == "__main__":
    obs_receiver = ObservationReceiver()
    obs_receiver.start()

    sender.send_command(10, 0)
    car_controls = [{"steer": 255, "throttle": 255} for _ in range(NUM_CARS)]

    try:
        frame_counter = 0  # simple counter for filenames

        while True:
            observations = []
            while not observations:
                observations = obs_receiver.collect_observations()
                if not observations:
                    time.sleep(0.001)

            # --- Process observations ---
            for obs in observations:
                frame_counter += 1

                # Print reward
                print(f"Car {obs.car_id} | Reward: {obs.reward:.3f}")

                # Convert to BGR for OpenCV display and saving
                img_bgr = cv2.cvtColor(obs.image, cv2.COLOR_RGB2BGR)

                # Display image
                cv2.imshow(f"Car {obs.car_id} Camera", img_bgr)
                cv2.waitKey(1)

                # Save image to /cameraTest/
                filename = f"car{obs.car_id}_frame{frame_counter:05d}_reward{obs.reward:.3f}.jpg"
                filepath = os.path.join(OUTPUT_DIR, filename)
                cv2.imwrite(filepath, img_bgr)

            # --- Send instructions back to Unity ---
            for i, ctrl in enumerate(car_controls):
                sender.send_car_instruction(
                    car_index=i,
                    steering=ctrl["steer"],
                    throttle=ctrl["throttle"]
                )

            time.sleep(0.0001)

    except KeyboardInterrupt:
        print("Exiting...")
        cv2.destroyAllWindows()
        sys.exit(0)
