using UnityEngine;

public class AgentInputProvider : MonoBehaviour, ICarInputProvider
{
    private CarInput currentInput;

    // ICarInputProvider implementation
    public CarInput getInput() => currentInput;

    public void SetInput(CarInput input)
    {
        currentInput = input;
    }
}