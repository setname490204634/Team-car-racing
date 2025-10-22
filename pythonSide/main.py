import sys
import time
import sender
from reciever import ObservationReceiver
import threading

# --- Configuration ---
NUM_CARS = 2  # change this to the number of cars in Unity
STEERING_NEUTRAL = 128
THROTTLE_NEUTRAL = 128

if __name__ == "__main__":
    # Start observation receiver in background thread
    obs_receiver = ObservationReceiver()
    obs_receiver.start()

    # Optionally connect transmitter (if you have a command for that)
    sender.send_command(10, 0)

    # Initialize a dummy control state for each car
    car_controls = [{"steer": 255, "throttle": 255} for _ in range(NUM_CARS)]

    try:
        while True:
            # --- Wait until we have at least one observation per car ---
            observations = []
            while not observations:
                observations = obs_receiver.collect_observations()  # collect and empty buffer
                if not observations:
                    time.sleep(0.001)  # very short sleep to yield CPU

            # --- Process observations ---
            for obs in observations:
                # Currently do nothing; could add AI or logging here
                pass

            # --- Send instructions back to Unity ---
            for i, ctrl in enumerate(car_controls):
                sender.send_car_instruction(
                    car_index=i,
                    steering=ctrl["steer"],
                    throttle=ctrl["throttle"]
                )

            # Optional: throttle loop slightly to avoid CPU burn
            time.sleep(0.0001)  # minimal sleep

    except KeyboardInterrupt:
        print("Exiting...")
        sys.exit(0)
