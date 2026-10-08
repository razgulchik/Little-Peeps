using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // A yes/no question over whatever is on screen. ONE instance on the Canvas, reused by every caller:
    // the caller brings the words and what each answer does, the dialog brings the look and the clicks.
    // A confirmation that needs content of its own (icons, a breakdown of points) is a screen, not this.
    //
    // It deliberately does not pause the game and does not know what it is asking about. Pausing is the
    // caller's call — PrestigeMenuState freezes the run around its question, a question asked in build
    // mode would have nothing to freeze — and that ignorance is what lets one prefab serve every question.
    //
    // Its visibility is its own and NOT a row in UIVisibility: a question can come up over any mode, so
    // "is it on screen" means "is a question pending", which no mode can say. The object must stay ACTIVE
    // for the same reason PerkSelectionUI must — the CanvasGroup is what hides it — and it belongs LAST
    // among the Canvas's children, so it draws over everything and its backdrop swallows the clicks meant
    // for whatever lies underneath.
    [RequireComponent(typeof(CanvasGroup))]
    public class ConfirmDialog : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        [Tooltip("Optional. Its text in the prefab is the default; a caller may pass another one.")]
        [SerializeField] private TMP_Text yesLabel;

        [Tooltip("Optional. Its text in the prefab is the default; a caller may pass another one.")]
        [SerializeField] private TMP_Text noLabel;

        private CanvasGroup group;
        private Action onYes;
        private Action onNo;

        // The labels as authored, read before any caller can overwrite them: passing no label must mean
        // the prefab's words, not whatever the previous question happened to say.
        private string defaultYes;
        private string defaultNo;
        private bool defaultsRead;

        public bool IsOpen { get; private set; }

        // Lazy, so Show works before Awake has run — Edit Mode tests never run it at all.
        private CanvasGroup Group => group != null ? group : group = GetComponent<CanvasGroup>();

        private void Awake()
        {
            ReadDefaults();
            if (!IsOpen) UIVisibility.Set(Group, false);
        }

        private void OnEnable()
        {
            if (yesButton != null) yesButton.onClick.AddListener(Confirm);
            if (noButton != null) noButton.onClick.AddListener(Cancel);
        }

        private void OnDisable()
        {
            if (yesButton != null) yesButton.onClick.RemoveListener(Confirm);
            if (noButton != null) noButton.onClick.RemoveListener(Cancel);
        }

        // Wiring tripwire, in Start so every Awake has had its chance first. A dialog that cannot show its
        // question or take an answer would leave whoever asked waiting — PrestigeMenuState with the game
        // frozen under it.
        private void Start()
        {
            if (messageText == null || yesButton == null || noButton == null)
                Debug.LogError($"[ConfirmDialog] '{name}' is missing its message text or a button — a " +
                               "question asked through it cannot be read or answered.", this);
        }

        // Ask. The answer arrives as exactly one call to onYes or onNo (either may be null), made after the
        // dialog has already closed — see Answer. A label left null shows the prefab's own.
        //
        // Returns whether the question is actually on screen. False only when the object is inactive,
        // where the CanvasGroup cannot show anything: a caller that freezes the game around its question
        // must then not freeze it, or the player is left with no way to answer.
        public bool Show(string message, Action onYes, Action onNo, string yesText = null, string noText = null)
        {
            if (!gameObject.activeInHierarchy)
            {
                Debug.LogError($"[ConfirmDialog] '{name}' is inactive, so \"{message}\" cannot be asked. Keep " +
                               "the object active — its CanvasGroup is what hides it.", this);
                return false;
            }

            ReadDefaults();

            if (IsOpen)
                Debug.LogWarning($"[ConfirmDialog] asked \"{message}\" while another question was still open — " +
                                 "the earlier one is dropped unanswered.", this);

            this.onYes = onYes;
            this.onNo = onNo;

            if (messageText != null) messageText.text = message;
            if (yesLabel != null) yesLabel.text = yesText ?? defaultYes;
            if (noLabel != null) noLabel.text = noText ?? defaultNo;

            IsOpen = true;
            UIVisibility.Set(Group, true);
            return true;
        }

        // Close without answering. Drops both callbacks, so a click that lands after this reaches nobody.
        // Safe to call when already closed: a state that asked calls it from its Exit, whichever way it is
        // left, so a late click can never call into a state that is no longer current.
        public void Hide()
        {
            onYes = null;
            onNo = null;
            IsOpen = false;
            UIVisibility.Set(Group, false);
        }

        // The two answers — what the buttons call, public so a key can give them too.
        public void Confirm() => Answer(onYes);
        public void Cancel() => Answer(onNo);

        // Close FIRST, then call. The callback is the caller's continuation, and it is free to ask the next
        // question or to leave its state (whose Exit calls Hide): closing afterwards would undo the first
        // and is pointless after the second. It is also what makes an answer one-shot — Hide has dropped
        // both callbacks before either runs, so a double click, or Yes and No in the same frame, counts once.
        private void Answer(Action choice)
        {
            if (!IsOpen) return;

            Hide();
            choice?.Invoke();
        }

        private void ReadDefaults()
        {
            if (defaultsRead) return;
            defaultsRead = true;

            defaultYes = yesLabel != null ? yesLabel.text : null;
            defaultNo = noLabel != null ? noLabel.text : null;
        }
    }
}
