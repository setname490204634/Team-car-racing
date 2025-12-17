from enum import IntEnum
# numbers in this file are based on protocol described in documentation
# constants in unity should match this file
class CommandCode(IntEnum):
    Reset = 0
    ShuffleCars = 1
    SetLapCount = 2
    ChangeMap = 3
    ChangeMapRandom = 4
    ChangeCarColoursRandomly = 5
    ResetCarToRandomStartLocation = 6

    StartSimulation = 10
    StopSimulation = 11
    ContinueSimulation = 12

    UpdateDeltaTime = 20
    SetFramesPerObservation = 21
    SetMaxSteeringChange = 22

    RealtimeSpeed = 30
    UnlimitedSpeed = 31
