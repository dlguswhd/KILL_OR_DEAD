/*
 *
*/
using UnityEngine;

namespace KillOrDead.Interaction
{
    public enum InputType 
    {
        TAP = 0,
        HOLD = 1,
    }
}

public class InteractionOptionsController : MonoBehaviour
{
    [SerializeField]
    private KillOrDead.Interaction.InputType _inputType;
    public KillOrDead.Interaction.InputType InputType { get => _inputType; private set => _inputType = value; }
    
    private string _menuText;
    public string MenuText { get => _menuText; private set => _menuText = value; }
    
    void Start()
    {
        
    }

    void Update()
    {
        
    }
}
