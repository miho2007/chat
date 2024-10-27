using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static List<(TcpClient Client, string Nickname)> clients = new List<(TcpClient, string)>(); // Store clients with nicknames

    static async Task Main(string[] args)
    {
        while (true)
        {
            Console.Write("/$~ ");
            string command = Console.ReadLine();

            try
            {
                if (command == "chat -serve")
                {
                    int port = 12345;
                    // Call StartGroupServerAsync if you want group functionality in the same port
                    await StartGroupServerAsync(port); // You can replace this with another method if needed
                }
                else if (command == "chat -serve group")
                {
                    int port = 12345;
                    await StartGroupServerAsync(port);
                }
                else if (command.StartsWith("chat -connect "))
                {
                    string ip = command.Split(' ')[2];
                    int port = 12345;
                    await StartClientAsync(ip, port);
                }
                else if (command == "chat -listIp")
                {
                    ListAvailableIPs();
                }
                else
                {
                    throw new ArgumentException("Invalid command.");
                }
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"{ex.Message} Try again.");
                DisplayHelp();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }

    // Start group server
    static async Task StartGroupServerAsync(int port)
    {
        IPAddress ipAddress = GetLocalIPAddress();
        TcpListener listener = new TcpListener(ipAddress, port);
        listener.Start();
        Console.WriteLine($"Group server started on IP {ipAddress} and port {port}. Waiting for clients...");

        // Start a separate task for the server to send messages
        _ = Task.Run(() => ServerSendMessagesAsync());

        while (true)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();

            // Get nickname from the new client
            var nickname = await GetNicknameAsync(client);
            clients.Add((client, nickname)); // Add client with nickname

            Console.WriteLine($"New client connected with nickname '{nickname}'.");

            // Handle client in a separate task
            _ = Task.Run(() => HandleGroupClientAsync(client, nickname));
        }
    }

    // Server can send messages to clients
    static async Task ServerSendMessagesAsync()
    {
        while (true)
        {
            string message = Console.ReadLine();
            if (!string.IsNullOrEmpty(message))
            {
                string serverMessage = $"[Server]: {message}";
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(serverMessage);
                Console.ResetColor();

                // Broadcast the server's message to all clients
                await BroadcastMessageAsync(serverMessage, null);
            }
        }
    }

    // Get nickname from the client
    static async Task<string> GetNicknameAsync(TcpClient client)
    {
        var writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };
        var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
        
        await writer.WriteLineAsync("Enter your nickname:");
        return await reader.ReadLineAsync();
    }

    // Handle messages from each client in group chat
    static async Task HandleGroupClientAsync(TcpClient client, string nickname)
    {
        using (var reader = new StreamReader(client.GetStream(), Encoding.UTF8))
        {
            while (true)
            {
                try
                {
                    string message = await reader.ReadLineAsync();
                    if (message == null) break;

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"{nickname}: {message}");
                    Console.ResetColor();

                    // Broadcast the message to all connected clients
                    await BroadcastMessageAsync($"{nickname}: {message}", client);
                }
                catch
                {
                    break;
                }
            }
        }

        // Remove client from list when disconnected
        clients.RemoveAll(c => c.Client == client);
        Console.WriteLine($"{nickname} disconnected.");
    }

    // Broadcast a message to all connected clients except the sender
    static async Task BroadcastMessageAsync(string message, TcpClient sender)
    {
        foreach (var (client, _) in clients)
        {
            if (client != sender) // Don't send message back to the sender
            {
                try
                {
                    var writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    await writer.WriteLineAsync(message);
                    Console.ResetColor();
                }
                catch
                {
                    // Remove client if sending fails
                    clients.RemoveAll(c => c.Client == client);
                }
            }
        }
    }

    static async Task StartClientAsync(string ip, int port)
    {
        try
        {
            TcpClient client = new TcpClient();
            await client.ConnectAsync(ip, port);
            Console.WriteLine($"Connected to server at {ip}:{port}");

            var writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };

            // Send nickname
            Console.Write("Enter your nickname: ");
            string nickname = Console.ReadLine();
            await writer.WriteLineAsync(nickname);

            var sendTask = Task.Run(() => SendMessageAsync(client));
            var receiveTask = Task.Run(() => ReceiveMessageAsync(client));

            await Task.WhenAll(sendTask, receiveTask);
        }
        catch (SocketException)
        {
            Console.WriteLine("Could not connect to the server. Ensure the IP address and port are correct.");
        }
    }

    static async Task SendMessageAsync(TcpClient client)
    {
        using (var writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true })
        {
            while (true)
            {
                string message = Console.ReadLine();
                if (message == null) break;

                Console.ForegroundColor = ConsoleColor.Blue;
                await writer.WriteLineAsync(message);
                Console.ResetColor();
            }
        }
    }

    static async Task ReceiveMessageAsync(TcpClient client)
    {
        using (var reader = new StreamReader(client.GetStream(), Encoding.UTF8))
        {
            while (true)
            {
                string message = await reader.ReadLineAsync();
                if (message == null) break;

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }
    }

    static void ListAvailableIPs()
    {
        string hostName = Dns.GetHostName();
        IPAddress[] ips = Dns.GetHostAddresses(hostName);

        Console.WriteLine("Available IPs:");
        foreach (IPAddress ip in ips)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                Console.WriteLine(ip.ToString());
            }
        }
    }

    static IPAddress GetLocalIPAddress()
    {
        foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip;
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }

    static void DisplayHelp()
    {
        Console.WriteLine("Available commands: ");
        Console.WriteLine("  chat -serve        : Start a single-client chat server.");
        Console.WriteLine("  chat -serve group  : Start a group chat server.");
        Console.WriteLine("  chat -connect <IP> : Connect to a chat server.");
        Console.WriteLine("  chat -listIp       : List available IPs on the host machine.");
    }
}
