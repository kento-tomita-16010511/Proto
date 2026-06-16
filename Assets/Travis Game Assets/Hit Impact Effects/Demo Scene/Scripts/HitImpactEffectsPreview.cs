using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem; // スクリプトの最上部に追加
#endif

namespace TravisGameAssets
{

	public class HitImpactEffectsPreview : MonoBehaviour
	{

		public Collider floorCollider;
		public Transform particlesPool;

		public Text hitNameLabel;
		public Text hitIndexLabel;

		public Transform cameraPivot;
		public float cameraRotationSpeed = 10f;

		public MeshRenderer floor;

		public Image rotationIcon;
		public Image floorIcon;
		public Image slowMotionIcon;

		public GameObject light;

		private GameObject[] hitEffects;

		private int hitIndex;

		private Vector3 initCamPosition;
		private Quaternion initCamRotation;

		private float initFov;
		private float minFov = 15f;
		private float maxFov = 90f;
		private float sensitivity = 10f;

		private bool cameraRotating;
		private bool floorVisible;
		private bool slowMotion;
		private bool lighting;

		void Start()
		{
			hitIndex = 0;
			cameraRotating = false;
			floorVisible = true;
			slowMotion = false;
			lighting = true;

			initFov = Camera.main.fieldOfView;
			initCamPosition = Camera.main.transform.position;
			initCamRotation = Camera.main.transform.rotation;

			hitEffects = new GameObject[particlesPool.childCount];

			for (int i = 0; i < particlesPool.childCount; i++)
			{
				hitEffects[i] = particlesPool.GetChild(i).gameObject;
			}

			RefreshHitUI();
		}

		void Update()
		{
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
			var keyboard = Keyboard.current;
			var mouse = Mouse.current;

			if (keyboard != null)
			{
				if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) PreviousHit();
				if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) NextHit();
				if (keyboard.digit1Key.wasPressedThisFrame) ToggleRotation();
				if (keyboard.digit2Key.wasPressedThisFrame) ToggleFloor();
				if (keyboard.digit3Key.wasPressedThisFrame) ToggleSlowMotion();
				if (keyboard.spaceKey.wasPressedThisFrame) ToggleLighting();
			}

			if (mouse != null)
			{
				// 左クリックでのエフェクト生成
				if (mouse.leftButton.wasPressedThisFrame)
				{
					if (!EventSystem.current.IsPointerOverGameObject())
					{
						RaycastHit hit = new RaycastHit();
						if (floorCollider.Raycast(Camera.main.ScreenPointToRay(mouse.position.ReadValue()), out hit, 1000f))
						{
							GameObject newHits = SpawnHit();
							newHits.transform.position = hit.point + newHits.transform.position;
						}
					}
				}

				// マウスホイールでのFOV調整
				// 新システムは値が大きいため 0.01f を掛けて調整
				float scroll = mouse.scroll.ReadValue().y * 0.01f;
				if (Mathf.Abs(scroll) > 0.001f)
				{
					float fov = Camera.main.fieldOfView;
					fov -= scroll * sensitivity;
					fov = Mathf.Clamp(fov, minFov, maxFov);
					Camera.main.fieldOfView = fov;
				}

				// 右クリックでカメラリセット
				if (mouse.rightButton.wasPressedThisFrame)
				{
					Camera.main.transform.position = initCamPosition;
					Camera.main.transform.rotation = initCamRotation;
					if (cameraRotating) ToggleRotation();
				}

				// 中クリックでFOVリセット
				if (mouse.middleButton.wasPressedThisFrame)
				{
					Camera.main.fieldOfView = initFov;
				}
			}
#else
			// 従来の入力システム
			if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) PreviousHit();
			if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) NextHit();

			if (Input.GetMouseButtonDown(0))
			{
				if (!EventSystem.current.IsPointerOverGameObject())
				{
					RaycastHit hit = new RaycastHit();
					if (floorCollider.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, 1000f))
					{
						GameObject newHits = SpawnHit();
						newHits.transform.position = hit.point + newHits.transform.position;
					}
				}
			}

			if (Input.GetKeyDown("1")) ToggleRotation();
			if (Input.GetKeyDown("2")) ToggleFloor();
			if (Input.GetKeyDown("3")) ToggleSlowMotion();
			if (Input.GetKeyDown(KeyCode.Space)) ToggleLighting();

			float fov = Camera.main.fieldOfView;
			fov -= Input.GetAxis("Mouse ScrollWheel") * sensitivity;
			fov = Mathf.Clamp(fov, minFov, maxFov);
			Camera.main.fieldOfView = fov;

			if (Input.GetMouseButtonDown(1)) // 右クリック
			{
				Camera.main.transform.position = initCamPosition;
				Camera.main.transform.rotation = initCamRotation;
				if (cameraRotating) ToggleRotation();
			}

			if (Input.GetMouseButtonDown(2)) // 中クリック
			{
				Camera.main.fieldOfView = initFov;
			}
#endif

			if (cameraRotating)
			{
				cameraPivot.Rotate(Vector3.up * (cameraRotationSpeed * Time.deltaTime));
			}
		}


		//Buttons

		public void ToggleRotation()
		{
			cameraRotating = !cameraRotating;

			var newColor = rotationIcon.color;
			newColor.a = cameraRotating ? 1f : 0.33f;
			rotationIcon.color = newColor;
		}

		public void ToggleFloor()
		{
			floorVisible = !floorVisible;

			floor.enabled = floorVisible;

			var newColor = floorIcon.color;
			newColor.a = floorVisible ? 1f : 0.33f;
			floorIcon.color = newColor;
		}

		public void ToggleSlowMotion()
		{
			slowMotion = !slowMotion;
			if (slowMotion)
			{
				Time.timeScale = 0.5f;
			}
			else
			{
				Time.timeScale = 1.0f;
			}

			var newColor = slowMotionIcon.color;
			newColor.a = slowMotion ? 1f : 0.33f;
			slowMotionIcon.color = newColor;
		}

		public void ToggleLighting()
		{
			lighting = !lighting;
			light.SetActive(lighting);
		}


		public void NextHit()
		{
			hitIndex++;
			if (hitIndex >= hitEffects.Length)
			{
				hitIndex = 0;
			}
			RefreshHitUI();
		}

		public void PreviousHit()
		{
			hitIndex--;
			if (hitIndex < 0)
			{
				hitIndex = hitEffects.Length - 1;
			}
			RefreshHitUI();
		}

		private void RefreshHitUI()
		{
			hitNameLabel.text = hitEffects[hitIndex].name;
			hitIndexLabel.text = string.Format("{0}/{1}", (hitIndex + 1).ToString("00"), hitEffects.Length.ToString("00"));
		}


		//Spawn Hit

		private GameObject SpawnHit()
		{
			GameObject spawnedHit = Instantiate(hitEffects[hitIndex]);
			spawnedHit.transform.LookAt(Camera.main.transform);
			return spawnedHit;
		}

	}
}