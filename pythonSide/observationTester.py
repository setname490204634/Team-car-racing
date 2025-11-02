import time
import cv2
import os
import sender
from reciever import ObservationReceiver

# run unity first with default ports, this file will show and save the observations as pictures and write the rewards
OUTPUT_DIR = "cameraTest"
os.makedirs(OUTPUT_DIR, exist_ok=True)

if __name__ == "__main__":
    obs_receiver = ObservationReceiver()
    obs_receiver.start()

    # Send some constant command to start Unity
    sender.send_command(10, 0, 5005)

    frame_counter = 0

    try:
        while True:
            observations = obs_receiver.collect_observations()
            if not observations:
                time.sleep(0.001)
                continue

            for obs in observations:
                frame_counter += 1
                print(obs.rewards)

                # Convert RGB → BGR for OpenCV
                img_bgr = cv2.cvtColor(obs.image, cv2.COLOR_RGB2BGR)

                # Show the image
                cv2.imshow(f"Car {obs.car_id} Camera", img_bgr)
                cv2.waitKey(1)

                # Save image if you want
                filename = f"car{obs.car_id}_frame{frame_counter:05d}.jpg"
                cv2.imwrite(os.path.join(OUTPUT_DIR, filename), img_bgr)

            # Send constant instructions back
            sender.send_car_instruction(0, 255, 255, 5006)

            time.sleep(0.01)

    except KeyboardInterrupt:
        print("Exiting...")
        cv2.destroyAllWindows()
