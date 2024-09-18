using ETA;
using ETA_Implementation;
using UnityEngine;
namespace Samples
{
    /// <summary>
    /// Controller class for Dev Testing
    ///
    /// todo Must be removed after the development
    /// </summary>
    public class Controller : MonoBehaviour
    {
        private void Start()
        {
            //fps to 60
            // Application.targetFrameRate = 60;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                // Plane 로드
                foreach (string key in EtaSdk.Instance.GetItemClientList())
                {
                    ItemClient itemClient = EtaSdk.Instance.GetItemClient(key)!;
                    FuncCtrl.FuncCall(in itemClient, "Load");
                }
            }
            else if(Input.GetKeyDown(KeyCode.G))
            {
                foreach (string key in EtaSdk.Instance.GetItemClientList())
                {
                    ItemClient itemClient = EtaSdk.Instance.GetItemClient(key)!;
                    FuncCtrl.FuncCall(in itemClient, "Show");
                }
            }
        }
    }
}