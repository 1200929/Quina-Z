using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkMenu : MonoBehaviour
{
    private string ip = "127.0.0.1";

    private void OnGUI()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.IsListening) return;

        GUILayout.BeginArea(new Rect(20, 20, 220, 150));

        if (GUILayout.Button("Host"))
        {
            // aceita ligações de outros PCs da rede (e também locais)
            nm.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
            nm.StartHost();
        }

        ip = GUILayout.TextField(ip);

        if (GUILayout.Button("Join (cliente)"))
        {
            nm.GetComponent<UnityTransport>().SetConnectionData(ip, 7777);
            nm.StartClient();
        }

        GUILayout.EndArea();
    }
}