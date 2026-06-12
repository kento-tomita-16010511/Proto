using UnityEngine;
using TMPro;

public class TimerController : MonoBehaviour
{
    [SerializeField] private float startSeconds = 60f;

    private TMP_Text _text;
    private float _remaining;
    private bool _running;

    void Start()
    {
        _text = GetComponent<TMP_Text>();
        _remaining = startSeconds;
        _running = true;
        UpdateDisplay();
    }

    void Update()
    {
        if (!_running) return;

        _remaining -= Time.deltaTime;
        if (_remaining <= 0f)
        {
            _remaining = 0f;
            _running = false;
        }
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        _text.text = Mathf.CeilToInt(_remaining).ToString();
    }
}
