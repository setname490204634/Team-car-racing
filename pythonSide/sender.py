import socket
import sys
import time
import reciever

HOST = "127.0.0.1"        # Unity is running locally
CONTROLPORT = 5005        # must match controlPort in Unity
CARINSTRUCTIONPORT = 5006 # must match carInstructionsPort in Unity

def send_command(command_byte: int):
    """Send a single-byte command to Unity control server."""
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.connect((HOST, CONTROLPORT))
            s.sendall(bytes([command_byte]))
    except ConnectionRefusedError:
        print("Could not connect to Unity control server.")
    except Exception as e:
        print(f"Error: {e}")

def send_car_instruction(car_index: int, steering: int, throttle: int):
    """
    Send driving instructions to Unity car instructions server.
    - car_index: which car to control (0–255)
    - steering: steering value (0–255)
    - throttle: throttle value (0–255)
    """
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.connect((HOST, CARINSTRUCTIONPORT))
            packet = bytes([car_index, steering, throttle])
            s.sendall(packet)
    except ConnectionRefusedError:
        print("Could not connect to Unity car instructions server.")
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    send_command(int(50))
    reciever.receive_observations()
    while True:
        print("\nChoose command:")
        print("0 → Reset Cars")
        print("1 → Shuffle Cars")
        print("q → Quit")
        
        choice = input("Enter: ").strip()
        if choice == "q":
            sys.exit(0)
        elif choice in ("0", "1"):
            send_command(int(choice))
        else:
            print("Invalid choice. Try again.")
        
        time.sleep(0.2)
