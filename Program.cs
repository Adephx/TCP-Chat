using System.Net;
using System.Net.Sockets;
using System.Text;

TcpListener? _tcpListener = null;
TcpClient? _tcpClient = null;
bool _isRunning = true;
string configFile = "config.ini";
string currentInput = "";
int inputStartPosition;

Dictionary<string, ConsoleColor> userColors = [];
ConsoleColor[] availableColors = 
[
    ConsoleColor.Blue,
    ConsoleColor.Cyan,
    ConsoleColor.Green,
    ConsoleColor.Magenta,
    ConsoleColor.Yellow,
    ConsoleColor.Red,
    ConsoleColor.Gray,
    ConsoleColor.DarkGreen
];
int colorIndex = 0;

Console.ForegroundColor = ConsoleColor.Yellow;

(string serverIp, int sendPort, int listenPort, string username) config;

string GetValidNickname()
{
    const int maxLength = 16;

    while (true)
    {
        SetSystemMessageColor();
        Console.WriteLine($"Enter your username (letters and digits only, max {maxLength} characters):");

        SetUserInputColor();
        string input = Console.ReadLine() ?? string.Empty;

        if (input.All(char.IsLetterOrDigit) && input.Length <= maxLength)
            return input;

        SetSystemMessageColor();
        Console.WriteLine(input.Length > maxLength
            ? $"Invalid username! Maximum length is {maxLength} characters. Try again."
            : "Invalid username! Only letters and digits are allowed. Try again.");

        SetUserInputColor();
    }
}

string GetValidIPAddress()
{
    while (true)
    {
        SetSystemMessageColor();
        Console.WriteLine("Enter the server IP address (localhost for same PC):");

        SetUserInputColor();
        string input = Console.ReadLine() ?? string.Empty;

        if (IPAddress.TryParse(input, out _))
            return input;

        SetSystemMessageColor();
        Console.WriteLine("Invalid IP address! Please enter a valid IP address.");

        SetUserInputColor();
    }
}

int GetValidPort(string prompt)
{
    while (true)
    {
        SetSystemMessageColor();
        Console.WriteLine(prompt);

        SetUserInputColor();
        string input = Console.ReadLine() ?? "0";

        if (int.TryParse(input, out int port) && port >= 1 && port <= 65535)
            return port;

        SetSystemMessageColor();
        Console.WriteLine("Invalid port! Please enter a port number between 1 and 65535.");

        SetUserInputColor();
    }
}

if (!File.Exists(configFile))
{
    config = (string.Empty, 0, 0, string.Empty);
    SetSystemMessageColor();
    Console.WriteLine("Config file not found, please enter the required details.");
    SetUserInputColor();
}
else
{
    config = ReadConfig();
}

if (string.IsNullOrEmpty(config.serverIp) || config.sendPort == 0 || config.listenPort == 0 || string.IsNullOrEmpty(config.username))
{
    config.username = GetValidNickname();
    config.serverIp = GetValidIPAddress();
    config.sendPort = GetValidPort("Enter the sending port:");
    config.listenPort = GetValidPort("Enter the listening port for incoming messages:");
    SaveConfig(config.serverIp, config.sendPort, config.listenPort, config.username);
}

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    _isRunning = false;
    SetSystemMessageColor();
    Console.WriteLine("\nExiting...");
    SetUserInputColor();
};

Thread listenerThread = new(() => ListenForMessages(config.listenPort));
listenerThread.Start();

_tcpClient = new TcpClient();
if (!IPAddress.TryParse(config.serverIp, out IPAddress? serverAddress))
{
    ShowSystemMessage("Invalid IP address.");
    return;
}

IPEndPoint serverEndPoint = new(serverAddress, config.sendPort);

SetSystemMessageColor();
Console.WriteLine("Press Ctrl+C to quit.");
SetUserInputColor();

ShowInputPrompt();

while (_isRunning)
{
    try
    {
        HandleUserInput(serverEndPoint);
    }
    catch (Exception ex)
    {
        ShowSystemMessage($"Error: {ex.Message}");
    }
}

_tcpListener?.Stop();
_tcpClient?.Close();

void HandleUserInput(IPEndPoint serverEndPoint)
{
    while (Console.KeyAvailable || _isRunning)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.WriteLine($"{config.username}: {currentInput.Trim()}");
                SendMessage(serverEndPoint, currentInput.Trim());

                currentInput = "";
                ShowInputPrompt();
            }
            else if (key.Key == ConsoleKey.Backspace && currentInput.Length > 0 && Console.CursorLeft > inputStartPosition)
            {
                currentInput = currentInput.Remove(currentInput.Length - 1);
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                Console.Write(" ");
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
            }
            else if (key.Key != ConsoleKey.Backspace && Console.CursorLeft >= inputStartPosition)
            {
                currentInput += key.KeyChar;
                Console.Write(key.KeyChar);
            }
        }
    }
}

void SendMessage(IPEndPoint serverEndPoint, string message)
{
    try
    {
        using TcpClient sendClient = new();
        sendClient.Connect(serverEndPoint);
        using NetworkStream stream = sendClient.GetStream();

        byte[] data = Encoding.UTF8.GetBytes($"{config.username}: {message}");
        stream.Write(data, 0, data.Length);
    }
    catch (Exception ex)
    {
        ShowSystemMessage($"Send error: {ex.Message}");
        currentInput = "";
        ClearCurrentConsoleLine();
    }
}

void ListenForMessages(int listenPort)
{
    try
    {
        _tcpListener = new TcpListener(IPAddress.Any, listenPort);
        _tcpListener.Start();

        while (_isRunning)
        {
            try
            {
                using TcpClient client = _tcpListener.AcceptTcpClient();
                using NetworkStream stream = client.GetStream();

                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);

                if (bytesRead > 0)
                {
                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    ShowReceivedMessage(receivedMessage);
                }
            }
            catch (Exception ex)
            {
                ShowSystemMessage($"Error receiving message: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        ShowSystemMessage($"Listener error: {ex.Message}");
    }
}

void ShowSystemMessage(string message)
{
    ClearCurrentConsoleLine();
    SetSystemMessageColor();
    Console.WriteLine(message);
    SetUserInputColor();
    ShowInputPrompt();
}

void ShowReceivedMessage(string message)
{
    string sender = message.Split(':')[0];

    if (!userColors.TryGetValue(sender, out ConsoleColor value))
    {
        value = availableColors[colorIndex % availableColors.Length];
        userColors[sender] = value;
        colorIndex++;
    }

    var previousColor = Console.ForegroundColor;

    ClearCurrentConsoleLine();

    Console.ForegroundColor = value;
    Console.WriteLine(message);

    SetUserInputColor();
    ShowInputPrompt();

    Console.Write(currentInput);
    Console.ForegroundColor = previousColor;
}

void ShowInputPrompt()
{
    Console.Write($"{config.username}: ");
    inputStartPosition = Console.CursorLeft;
}

void SaveConfig(string serverIp, int sendPort, int listenPort, string username)
{
    using StreamWriter writer = new(configFile);
    writer.WriteLine($"ServerIP={serverIp}");
    writer.WriteLine($"SendPort={sendPort}");
    writer.WriteLine($"ListenPort={listenPort}");
    writer.WriteLine($"Username={username}");
}

(string serverIp, int sendPort, int listenPort, string username) ReadConfig()
{
    string serverIp = "";
    int sendPort = 0, listenPort = 0;
    string username = "";

    foreach (var line in File.ReadAllLines(configFile))
    {
        var parts = line.Split('=');
        if (parts.Length == 2)
        {
            switch (parts[0])
            {
                case "ServerIP": serverIp = parts[1]; break;
                case "SendPort": _ = int.TryParse(parts[1], out sendPort); break;
                case "ListenPort": _ = int.TryParse(parts[1], out listenPort); break;
                case "Username": username = parts[1]; break;
            }
        }
    }
    return (serverIp, sendPort, listenPort, username);
}

void SetUserInputColor() => Console.ForegroundColor = ConsoleColor.Yellow;

void SetSystemMessageColor() => Console.ForegroundColor = ConsoleColor.Gray;

void ClearCurrentConsoleLine()
{
    Console.SetCursorPosition(0, Console.CursorTop);
    Console.Write(new string(' ', Console.WindowWidth));
    Console.SetCursorPosition(0, Console.CursorTop);
}