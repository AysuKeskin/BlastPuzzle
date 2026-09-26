using System;
using TMPro;
using UnityEngine;

namespace BlastPuzzle.UI
{
    // Shared layout; each screen supplies its own actions and state.
    public sealed class SettingsPanelView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button soundButton;
        [SerializeField] private UnityEngine.UI.Button hapticsButton;
        [SerializeField] private UnityEngine.UI.Button primaryButton;
        [SerializeField] private UnityEngine.UI.Button closeButton;
        [SerializeField] private TMP_Text soundLabel;
        [SerializeField] private TMP_Text hapticsLabel;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private TMP_Text closeLabel;

        public void Configure(Action sound, Action haptics, Action primary, Action close, string closeText)
        {
            soundButton.onClick.AddListener(() => sound());
            hapticsButton.onClick.AddListener(() => haptics());
            primaryButton.onClick.AddListener(() => primary());
            closeButton.onClick.AddListener(() => close());
            closeLabel.text = closeText;
            gameObject.SetActive(false);
        }

        public void Refresh(bool sound, bool haptics, string primaryText)
        {
            soundLabel.text = sound ? "SOUND: ON" : "SOUND: OFF";
            hapticsLabel.text = haptics ? "VIBRATION: ON" : "VIBRATION: OFF";
            primaryLabel.text = primaryText;
        }

        public void Show()
        {
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
