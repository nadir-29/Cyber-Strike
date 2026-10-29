using UnityEngine;
using UnityEngine.InputSystem;

// Reads keyboard/mouse input and forwards it to the Fighter.
[RequireComponent(typeof(Fighter))]
public class PlayerInputController : MonoBehaviour
{
    Fighter fighter;
    InputAction moveAction;
    InputAction jumpAction;

    void Awake()
    {
        fighter = GetComponent<Fighter>();

        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/a")
            .With("Positive", "<Keyboard>/d");

        jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
    }

    void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
        fighter.SetMoveInput(0f);
    }

    void OnDestroy()
    {
        moveAction.Dispose();
        jumpAction.Dispose();
    }

    void Update()
    {
        fighter.SetMoveInput(moveAction.ReadValue<float>());

        if (jumpAction.WasPressedThisFrame())
            fighter.Jump();
    }
}
