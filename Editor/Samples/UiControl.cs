using UnityEngine;

public class UiControl : MonoBehaviour
{
    public Canvas uGui;
    public Canvas uiToolkitGui;

    // Update is called once per frame
    void Update()
    {

        if (Input.GetKeyDown(KeyCode.U))
        {
            bool current = uGui.enabled;
            uGui.enabled = !current;
            uGui.gameObject.SetActive(!current);
        }
        else if (Input.GetKeyDown(KeyCode.T))
        {
            bool current = uiToolkitGui.enabled;
            uiToolkitGui.enabled = !current;
            uiToolkitGui.gameObject.SetActive(!current);
        }
    }
}
