using System;
using System.IO;
using System.Net;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class UnityMcpServerBridge
{
    private static HttpListener listener;

    static UnityMcpServerBridge()
    {
        // 無料版MCPサーバーが探しに来るポート29293番でサーバーを起動
        listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:29293/");
        listener.Start();
        listener.BeginGetContext(OnRequest, null);
        Debug.Log("[Unity MCP] 完全無料版・JSON-RPC互換ブリッジが起動しました。");
    }

    private static void OnRequest(IAsyncResult result)
    {
        if (!listener.IsListening) return;
        var context = listener.EndGetContext(result);
        listener.BeginGetContext(OnRequest, null);

        var request = context.Request;
        var response = context.Response;

        string responseString = "{\"jsonrpc\":\"2.0\",\"result\":{\"status\":\"success\"},\"id\":1}";

        // 1. 無料版MCPサーバーが通信のチェックやインスタンス確認（GET/POST）をしに来たとき
        if (request.HttpMethod == "GET" || request.Url.PathAndQuery.Contains("list") || request.Url.PathAndQuery.Contains("instances"))
        {
            // MCPサーバーが完璧に認識する「JSON-RPC 2.0 形式」の応答データを返します
            responseString = "{\n" +
                             "  \"jsonrpc\": \"2.0\",\n" +
                             "  \"id\": 1,\n" +
                             "  \"result\": {\n" +
                             "    \"instances\": [\n" +
                             "      {\n" +
                             "        \"port\": 29293,\n" +
                             "        \"projectName\": \"" + Application.productName + "\",\n" +
                             "        \"unityVersion\": \"" + Application.unityVersion + "\"\n" +
                             "      }\n" +
                             "    ]\n" +
                             "  }\n" +
                             "}";
        }
        // 2. Clineから「Cubeを作れ」などの具体的な命令（POST）が飛んできたとき
        else if (request.HttpMethod == "POST")
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                string jsonCommand = reader.ReadToEnd();
                
                // Unityのメインスレッドで安全にCubeを作成
                EditorApplication.delayCall += () => {
                    ExecuteCommandFromCline(jsonCommand);
                };
            }
        }

        // 適切なヘッダーを設定してMCPサーバーに応答を返送
        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentType = "application/json";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }

    private static void ExecuteCommandFromCline(string json)
    {
        // 命令（JSON）の中に作成を意味するキーワードが含まれている場合
        if (json.Contains("create") || json.Contains("Cube") || json.Contains("cube") || json.Contains("gameobject"))
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Cline_Generated_Cube";
            cube.transform.position = Vector3.zero;
            Debug.Log("[Unity MCP] Clineからの正式なJSON-RPC信号を受信！シーンにCubeを作成しました。");
        }
    }
}
