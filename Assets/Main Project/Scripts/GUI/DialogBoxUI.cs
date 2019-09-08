using UnityEngine;
using System;

public delegate void DialogBoxAction();

public class DialogBoxUI : MonoBehaviour {
    [Serializable]
    public class DialogBoxElement {
        public enum ConstrictDirection { Disabled = 0, Horizontal = 1, Vertical = 2};

        public UIWidget widget;
        public ConstrictDirection constrictDirection = ConstrictDirection.Disabled;
    }

    public static DialogBoxUI instance { get; private set; }

    public DialogBoxAction onConfirmAction;
    public DialogBoxAction onCancelAction;
    
    public GameObject cachedGo;
    public Transform cachedTrans;
    public UIPanel dialogPanel;
    public UIWidget dialogGroup;
    public UISprite windowBackground;
    public UIEventListener cancelBgListener; // usually just a fullscreen backdrop collider
    public UILabel titleTextLabel;
    public UILabel bodyTextLabel;
    public UIButton confirmButton;
    public UILabel confirmButtonLabel;
    public UIButton cancelButton;
    public UILabel cancelButtonLabel;
    public DialogBoxElement windowBar;
    public DialogBoxElement graphicSprite;
    public DialogBoxElement confirmButtonSprite;
    public DialogBoxElement cancelButtonSprite;
    public float buttonSpacing = 100f;
    public float fadeTime = 0.1f;
    public Vector2 bodyMargin = new Vector2(10f, 8f);
    
    public bool isVisible {
        get {
            if(dialogGroup != null)
                return (dialogGroup.alpha > LutonUtils.EPSILON);
            else if(dialogPanel != null)
                return (dialogPanel.alpha > LutonUtils.EPSILON);

            return false;
        }
    }

    public bool isDisplaying { get; private set; }

    private UIEventListener confirmButtonEventListener;
    private UIEventListener cancelButtonEventListener;
    private Vector4 bodyTextArea;
    private int visibleFrames;

    private void Awake() {
        if(instance != null) {
            Debug.LogError("There can only be one DialogBoxUI active at a time!");
            Destroy(gameObject);
            return;
        }

        instance = this;
        
        if(confirmButton != null)
            confirmButtonEventListener = confirmButton.GetComponent<UIEventListener>();
        if(cancelButton != null)
            cancelButtonEventListener = cancelButton.GetComponent<UIEventListener>();

        RecalculateTextBoundsAndUI();
        isDisplaying = false;
        cachedGo.SetActive(false);

        if(dialogPanel != null)
            dialogPanel.alpha = 0f;
        else if(dialogGroup != null)
            dialogGroup.alpha = 0f;
    }

    private void OnEnable() {
        if(confirmButtonEventListener != null)
            confirmButtonEventListener.onClick = CloseDialog_Confirm;
        if(cancelButtonEventListener != null)
            cancelButtonEventListener.onClick = CloseDialog_Cancel;
        if(cancelBgListener != null)
            cancelBgListener.onClick = CloseDialog_Cancel;
    }

    private void OnDisable() {
        if(confirmButtonEventListener != null)
            confirmButtonEventListener.onClick = null;
        if(cancelButtonEventListener != null)
            cancelButtonEventListener.onClick = null;
        if(cancelBgListener != null)
            cancelBgListener.onClick = null;
    }

    private void Update() {
        float targetAlpha = (isDisplaying) ? 1f : 0f;

        if(dialogGroup != null)
            dialogGroup.alpha = Mathf.MoveTowards(dialogGroup.alpha, targetAlpha, Time.unscaledDeltaTime / fadeTime);
        else if(dialogPanel != null)
            dialogPanel.alpha = Mathf.MoveTowards(dialogPanel.alpha, targetAlpha, Time.unscaledDeltaTime / fadeTime);

        if(isVisible) {
            visibleFrames++;
        }
        else {
            visibleFrames = 0;
            cachedGo.SetActive(false); // Disable dialog logic and visibility.
            return;
        }

        // Only check for input if it has been visible for more than a frame.
        if(visibleFrames > 1) {
            if(Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) {
                confirmButton.SendMessage("OnClick");
            }
            else if(Input.GetKeyDown(KeyCode.Escape)) {
                cancelButton.SendMessage("OnClick");
            }
        }
    }

    public void Display(string body, DialogBoxAction closeAction) {
        Display("ATTENTION", body, closeAction);
    }
    
    public void Display(string body, DialogBoxAction confirmAction, DialogBoxAction cancelAction) {
        Display("ATTENTION", body, confirmAction, cancelAction);
    }

    public void Display(string title, string body, DialogBoxAction closeAction) {
        confirmButton.gameObject.SetActive(false);
        ResetButtonLabelsToDefault();
        DisplayFullHelper(title, body, null, closeAction);
    }

    public void Display(string title, string body, DialogBoxAction confirmAction, DialogBoxAction cancelAction) {
        confirmButton.gameObject.SetActive(true);
        ResetButtonLabelsToDefault();
        DisplayFullHelper(title, body, confirmAction, cancelAction);
    }

    private void DisplayFullHelper(string title, string body, DialogBoxAction confirmAction, DialogBoxAction cancelAction) {
        cachedGo.SetActive(true);
        isDisplaying = true;
        RecalculateTextBoundsAndUI();
        titleTextLabel.text = title;
        bodyTextLabel.text = body;
        onConfirmAction = confirmAction;
        onCancelAction = cancelAction;
    }

    public void CloseAndReset() {
        if(!isDisplaying)
            return;

        isDisplaying = false;
        onConfirmAction = null;
        onCancelAction = null;
    }

    public void CloseDialog_Confirm(GameObject invoker) {
        if(!isDisplaying)
            return;

        isDisplaying = false;

        if(onConfirmAction != null)
            onConfirmAction();
    }

    public void CloseDialog_Cancel(GameObject invoker) {
        if(!isDisplaying)
            return;

        isDisplaying = false;

        if(onCancelAction != null)
            onCancelAction();
    }

    private void ResetButtonLabelsToDefault() {
        if(confirmButton != null && confirmButton.gameObject.activeSelf) {
            // OK and Cancel button.
            confirmButtonLabel.text = "OK";
            cancelButtonLabel.text = "Cancel";
        }
        else {
            // One button only.
            cancelButtonLabel.text = "OK";
        }
    }
    
    // L, R, B, T
    private Vector4 GetWidgetLocalBoundary(UIWidget w) {
        Vector3 localDiff = cachedTrans.InverseTransformVector(w.cachedTrans.position - cachedTrans.position);
        float pivotX = localDiff.x + ((w.pivotOffset.x - 0.5f) * w.width);
        float pivotY = localDiff.y - ((w.pivotOffset.y - 0.5f) * w.height);
        return new Vector4(pivotX - (w.width * 0.5f), pivotX + (w.width * 0.5f), pivotY - (w.height * 0.5f), pivotY + (w.height * 0.5f));
    }

    private Vector2 GetBoundaryCenter(Vector4 bounds) {
        return new Vector2((bounds.x + bounds.y) * 0.5f, (bounds.z + bounds.w) * 0.5f);
    }

    private Vector2 GetBoundaryDimensions(Vector4 bounds) {
        return new Vector2(bounds.y - bounds.x, bounds.w - bounds.z);
    }

    private void RecalculateTextBoundsAndUI() {
        bodyTextArea = GetWidgetLocalBoundary(windowBackground);
        ConstrictBodyArea(windowBar);

        if(graphicSprite.widget != null && graphicSprite.widget.enabled) {
            ConstrictBodyArea(graphicSprite);
        }

        ConstrictBodyArea(confirmButtonSprite);
        ConstrictBodyArea(cancelButtonSprite);
        bodyTextArea.x += bodyMargin.x;
        bodyTextArea.y -= bodyMargin.x;
        bodyTextArea.z += bodyMargin.y;
        bodyTextArea.w -= bodyMargin.y;

        bodyTextLabel.cachedTrans.localPosition = GetBoundaryCenter(bodyTextArea);
        Vector2 bodyDim = GetBoundaryDimensions(bodyTextArea);
        bodyTextLabel.width = (int)bodyDim.x;
        bodyTextLabel.height = (int)bodyDim.y;
        AdjustPositionForPivot();

        Vector3 confirmButtonPos = confirmButton.transform.localPosition;

        if(confirmButton != null && confirmButton.gameObject.activeSelf) {
            confirmButton.transform.localPosition = new Vector3(buttonSpacing, confirmButtonPos.y, confirmButtonPos.z);
            cancelButton.transform.localPosition = new Vector3(-buttonSpacing, confirmButtonPos.y, confirmButtonPos.z);
        }
        else {
            cancelButton.transform.localPosition = new Vector3(0f, confirmButtonPos.y, confirmButtonPos.z);
        }
    }

    private void AdjustPositionForPivot() {
        Vector3 labelPos = bodyTextLabel.cachedTrans.localPosition;
        float offX = bodyTextLabel.width * 0.5f;
        float offY = bodyTextLabel.height * 0.5f;

        switch(bodyTextLabel.pivot) {
            case UIWidget.Pivot.Left:
                labelPos.x -= offX;
                break;
            case UIWidget.Pivot.Right:
                labelPos.x += offX;
                break;
            case UIWidget.Pivot.Bottom:
                labelPos.y -= offY;
                break;
            case UIWidget.Pivot.Top:
                labelPos.y += offY;
                break;
            case UIWidget.Pivot.TopLeft:
                labelPos.x -= offX;
                labelPos.y += offY;
                break;
            case UIWidget.Pivot.TopRight:
                labelPos.x += offX;
                labelPos.y += offY;
                break;
            case UIWidget.Pivot.BottomLeft:
                labelPos.x -= offX;
                labelPos.y -= offY;
                break;
            case UIWidget.Pivot.BottomRight:
                labelPos.x += offX;
                labelPos.y -= offY;
                break;
        }

        bodyTextLabel.cachedTrans.localPosition = labelPos;
    }

    private void ConstrictBodyArea(DialogBoxElement element) {
        if(element.widget == null) {
            return;
        }

        Vector4 widgetBounds = GetWidgetLocalBoundary(element.widget);
        Vector2 dir = (GetBoundaryCenter(widgetBounds) - GetBoundaryCenter(bodyTextArea));

        if(element.constrictDirection == DialogBoxElement.ConstrictDirection.Horizontal) {
            if(dir.x >= 0f) {
                bodyTextArea.y = Mathf.Min(bodyTextArea.y, widgetBounds.x);
            }
            else {
                bodyTextArea.x = Mathf.Max(bodyTextArea.x, widgetBounds.y);
            }
        }

        if(element.constrictDirection == DialogBoxElement.ConstrictDirection.Vertical) {
            if(dir.y >= 0f) {
                bodyTextArea.w = Mathf.Min(bodyTextArea.w, widgetBounds.z);
            }
            else {
                bodyTextArea.z = Mathf.Max(bodyTextArea.z, widgetBounds.w);
            }
        }
    }
}