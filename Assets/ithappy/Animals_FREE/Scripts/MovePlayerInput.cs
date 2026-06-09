using UnityEngine;
using UnityEngine.InputSystem; // 新しいInput Systemを使用するために追加

namespace ithappy.Animals_FREE
{
    [RequireComponent(typeof(CreatureMover))]
    public class MovePlayerInput : MonoBehaviour
    {
        [Header("Character")]
        [SerializeField]
        private string m_HorizontalAxis = "Horizontal";
        [SerializeField]
        private string m_VerticalAxis = "Vertical";
        [SerializeField]
        private string m_JumpButton = "Jump";
        [SerializeField]
        private KeyCode m_RunKey = KeyCode.LeftShift;

        [Header("Camera")]
        [SerializeField]
        private PlayerCamera m_Camera;
        [SerializeField]
        private string m_MouseX = "Mouse X";
        [SerializeField]
        private string m_MouseY = "Mouse Y";
        [SerializeField]
        private string m_MouseScroll = "Mouse ScrollWheel";

        private CreatureMover m_Mover;

        private Vector2 m_Axis;
        private bool m_IsRun;
        private bool m_IsJump;

        private Vector3 m_Target;
        private Vector2 m_MouseDelta;
        private float m_Scroll;

        private void Awake()
        {
            m_Mover = GetComponent<CreatureMover>();
        }

        private void Update()
        {
            GatherInput();
            SetInput();
        }

        public void GatherInput()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            // 新しいInput Systemを使用する場合の入力取得
            Vector2 moveInput = Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveInput.x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveInput.x += 1f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveInput.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveInput.y -= 1f;
            }
            if (Gamepad.current != null)
            {
                moveInput += Gamepad.current.leftStick.ReadValue();
            }
            m_Axis = moveInput.normalized; // 斜め移動で速度が速くならないように正規化

#else
            // レガシーInput Managerを使用する場合の入力取得
            m_Axis = new Vector2(Input.GetAxis(m_HorizontalAxis), Input.GetAxis(m_VerticalAxis));
#endif
            // m_Target は Input クラスを使用しないため、条件付きコンパイルの外に置く
            m_Target = (m_Camera == null) ? Vector3.zero : m_Camera.Target;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            // 走る (m_IsRun)
            m_IsRun = (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) ||
                      (Gamepad.current != null && Gamepad.current.leftTrigger.isPressed); // LeftShiftキーまたはゲームパッドのLeftTriggerを想定

            // ジャンプ (m_IsJump)
            m_IsJump = (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) ||
                       (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed); // SpaceキーまたはゲームパッドのButtonSouthを想定

            // マウス入力
            m_MouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            m_Scroll = Mouse.current != null ? Mouse.current.scroll.y.ReadValue() : 0f;
#else
            m_IsRun = Input.GetKey(m_RunKey);
            m_IsJump = Input.GetButton(m_JumpButton);
            m_MouseDelta = new Vector2(Input.GetAxis(m_MouseX), Input.GetAxis(m_MouseY));
            m_Scroll = Input.GetAxis(m_MouseScroll);
#endif
        }

        public void BindMover(CreatureMover mover)
        {
            m_Mover = mover;
        }

        public void SetInput()
        {
            if (m_Mover != null)
            {
                m_Mover.SetInput(in m_Axis, in m_Target, in m_IsRun, m_IsJump);
            }

            if (m_Camera != null)
            {
                m_Camera.SetInput(in m_MouseDelta, m_Scroll);
            }
        }
    }
}