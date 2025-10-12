import sys
import time
import sender
import reciever
import threading

if __name__ == "__main__":
    # Start observation receiver in background thread
    receiver_thread = threading.Thread(target=reciever.receive_observations, daemon=True)
    receiver_thread.start()
    
    # Start Unity transmitter
    # sender.send_command(50, 0)


    while True:
        print("\nChoose command:")
        print("0 → Reset Cars")
        print("1 → Shuffle Cars")
        print("q → Quit")
        print("Or enter command and value separated by space, e.g. '1 5'")

        choice = input("Enter: ").strip()
        if choice.lower() == "q":
            sys.exit(0)

        parts = choice.split()
        if len(parts) == 0:
            print("Invalid input. Try again.")
            continue

        try:
            command_byte = int(parts[0])
            value_byte = int(parts[1]) if len(parts) > 1 else 0
            sender.send_command(command_byte, value_byte)
        except ValueError:
            print("Invalid input. Enter integers only.")
        except Exception as e:
            print(f"Error sending command: {e}")

        time.sleep(0.2)
