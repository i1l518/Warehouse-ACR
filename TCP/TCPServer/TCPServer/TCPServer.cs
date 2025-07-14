using System.Net;
using System.Net.Sockets;
using System.Text;
using ActUtlType64Lib;

namespace TCPServer
{
    class TCPServer
    {
        enum State
        {
            CONNECTED,
            DISCONNECTED
        }
        static ActUtlType64 mxComponent;
        static State state;

        // 로그 덮어쓰기를 위한 잠금 객체
        private static readonly object _consoleLock = new object();

        static async Task Main()
        {
            // MxComponent 초기설정
            mxComponent = new ActUtlType64();
            mxComponent.ActLogicalStationNumber = 1;
            state = State.DISCONNECTED;

            IPAddress addr = IPAddress.Parse("127.0.0.1");
            TcpListener server = new TcpListener(addr, 12345);
            server.Start();

            Console.Clear(); // 콘솔 화면을 깨끗이 지웁니다.
            Console.WriteLine("PLC 통신 서버가 시작되었습니다.");
            Console.WriteLine("클라이언트의 연결을 대기합니다...");
            Console.WriteLine("-----------------------------------");
            Console.WriteLine(); // 한 줄 띄우기
            // 로그가 표시될 초기 위치 설정
            Console.WriteLine("수신: (대기 중)");
            Console.WriteLine("송신: (대기 중)");

            while (true)
            {
                TcpClient client = await server.AcceptTcpClientAsync();

                lock (_consoleLock)
                {
                    // 커서를 기존 로그 아래로 이동하여 연결 메시지 출력
                    Console.SetCursorPosition(0, 8);
                    Console.WriteLine($"클라이언트 연결됨: {((IPEndPoint)client.Client.RemoteEndPoint).Address}");
                    // 다시 로그 표시 위치로 커서 초기화
                    Console.SetCursorPosition(0, 4);
                }

                Task.Run(() => HandleClientAync(client));
            }
        }

        static async Task HandleClientAync(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            Byte[] buffer = new byte[1024];
            int byteRead;

            const int RECV_LINE = 4; // 수신 로그는 5번째 줄에
            const int SEND_LINE = 5; // 송신 로그는 6번째 줄에

            try
            {
                bool isConnectedToServer = true;

                while (isConnectedToServer && (byteRead = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    string dataStr = Encoding.UTF8.GetString(buffer, 0, byteRead);

                    // 수신 로그 덮어쓰기
                    lock (_consoleLock)
                    {
                        // 1. 수신 로그 줄로 커서 이동
                        Console.SetCursorPosition(0, RECV_LINE);
                        // 2. 해당 줄을 공백으로 채워서 완전히 비움
                        Console.Write(new string(' ', Console.WindowWidth - 1));
                        // 3. 다시 커서를 맨 앞으로 이동
                        Console.SetCursorPosition(0, RECV_LINE);
                        // 4. 새로운 로그 출력
                        Console.Write($"수신: {dataStr}");
                    }

                    string result = FSM(dataStr);

                    if (result.Contains("Disconnected"))
                    {
                        isConnectedToServer = false;
                    }

                    byte[] responseData = Encoding.UTF8.GetBytes(result);
                    await stream.WriteAsync(responseData, 0, responseData.Length);

                    // [수정] 송신 로그 덮어쓰기
                    lock (_consoleLock)
                    {
                        // 1. 송신 로그 줄로 커서 이동
                        Console.SetCursorPosition(0, SEND_LINE);
                        // 2. 해당 줄을 공백으로 채워서 완전히 비움
                        Console.Write(new string(' ', Console.WindowWidth - 1));
                        // 3. 다시 커서를 맨 앞으로 이동
                        Console.SetCursorPosition(0, SEND_LINE);
                        // 4. 새로운 로그 출력
                        Console.Write($"송신: {result}");
                    }
                }
            }
            catch (Exception ex)
            {
                lock (_consoleLock)
                {
                    Console.SetCursorPosition(0, 9);
                    Console.WriteLine($"클라이언트 연결 끊김: {ex.Message}".PadRight(Console.WindowWidth - 1));
                    Console.SetCursorPosition(0, SEND_LINE + 1); // 커서를 로그 아래로 이동

                    Disconnect();
                }
            }
            finally
            {
                // --- [중요] 클라이언트와의 TCP 연결을 명시적으로 닫음 ---
                client.Close();
                lock (_consoleLock)
                {
                    Console.SetCursorPosition(0, 10);
                    Console.WriteLine($"클라이언트 소켓 연결 해제됨.".PadRight(Console.WindowWidth - 1));
                }
            }
        }


        private static string FSM(string dataStr)
        {
            if (dataStr.Contains("Connect"))
            {
                return Connect();
            }
            // 이 부분이 올바르게 Disconnect() 함수를 호출하는지 확인
            else if (dataStr.Contains("Disconnect"))
            {
                return Disconnect(); // 이 함수가 mxComponent.Close()를 호출함
            }
            else if (dataStr.StartsWith("Request,")) // Request,read,X10,1,read,Y0,1,write,X0,1,0
            {
                string[] commands = dataStr.Substring("Request,".Length).Split(',');
                List<string> responseParts = new List<string>();
                int i = 0;
                while (i < commands.Length)
                {
                    string commandType = commands[i];
                    if (commandType.Equals("read", StringComparison.OrdinalIgnoreCase))
                    {
                        string address = commands[i + 1];
                        string blockCnt = commands[i + 2];
                        string result = ReadDeviceBlock($"Request,read,{address},{blockCnt}");
                        responseParts.Add(result);
                        i += 3;
                    }
                    else if (commandType.Equals("write", StringComparison.OrdinalIgnoreCase))
                    {
                        string address = commands[i + 1];
                        string blockCnt = commands[i + 2];
                        string value = commands[i + 3];
                        string result = WriteDeviceBlock($"Request,write,{address},{blockCnt},{value}");
                        responseParts.Add(result);
                        i += 4;
                    }
                    else
                    {
                        responseParts.Add("Fail: Unknown command");
                        break;
                    }
                }
                return string.Join(",", responseParts); // Read,X10,1,0,Read,Y0,1,0,Write,X0,1,0
            }
            else
            {
                return "Fail: Invalid request format";
            }
        }

        private static string WriteDeviceBlock(string dataStr)
        {
            string[] data = dataStr.Split(',');
            string address = data[2];
            if (!int.TryParse(data[3], out int blockCnt))
                return "Request Error: BlockCnt 문자열 오류";

            if (!int.TryParse(data[4], out int value))
                return "Request Error: Device Value 문자열 오류";

            int[] values = new int[blockCnt];
            values[0] = value;
            int iRet = mxComponent.WriteDeviceBlock(address, blockCnt, ref values[0]);
            if (iRet == 0) return $"Write,{address},{blockCnt},{value}";
            else return "0x" + Convert.ToString(iRet, 16);
        }

        private static string ReadDeviceBlock(string dataStr)
        {
            string[] data = dataStr.Split(',');
            string address = data[2];
            if (!int.TryParse(data[3], out int blockCnt))
                return "Request Error: BlockCnt 문자열 오류";

            int[] newData = new int[blockCnt];
            int iRet = mxComponent.ReadDeviceBlock(address, blockCnt, out newData[0]);
            if (iRet == 0)
            {
                string str = "";
                for (int i = 0; i < newData.Length; i++) str += newData[i] + ",";
                str = str.TrimEnd(','); // 마지막 쉼표 제거
                return $"Read,{address},{blockCnt},{str}";
            }
            else return "0x" + Convert.ToString(iRet, 16);
        }

        private static string Disconnect()
        {
            // --- [중요] PLC 객체 해제 로직 ---
            int iRet = mxComponent.Close();
            if (iRet == 0)
            {
                state = State.DISCONNECTED;

                return "Disconnected";
            }
            else
            {
                // 16진수 오류 코드를 반환
                return "Disconnect Error: 0x" + Convert.ToString(iRet, 16);
            }
        }

        private static string Connect()
        {
            int iRet = mxComponent.Open();
            if (iRet == 0)
            {
                state = State.CONNECTED;
                return "Connected";
            }
            else return "0x" + Convert.ToString(iRet, 16);
        }
    }
}