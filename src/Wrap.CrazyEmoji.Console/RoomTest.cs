using Microsoft.AspNetCore.SignalR.Client;

public static class RoomTest
{
    public static async Task RunAsync()
    {
        Console.WriteLine("Starting Room Test Client...");

        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5026/roomHub")
            .WithAutomaticReconnect()
            .Build();

        connection.On<string>("RoomCreated", code => Console.WriteLine($"Room created: {code}"));
        connection.On<string>("JoinedRoom", code => Console.WriteLine($"Joined room: {code}"));
        connection.On<string, string>("ReceiveMessage", (id, msg) => Console.WriteLine($"{id}: {msg}"));

        await connection.StartAsync();

        Console.WriteLine("Connected!");

        Console.WriteLine("Type a command:");
        Console.WriteLine("1. create <ROOMCODE>");
        Console.WriteLine("2. join <ROOMCODE>");
        Console.WriteLine("3. send <ROOMCODE> <MESSAGE>");
        Console.WriteLine("4. exit");

        while (true)
        {
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            var parts = input.Split(' ', 3);
            var command = parts[0].ToLower();

            try
            {
                switch (command)
                {
                    case "create":
                        await connection.InvokeAsync("CreateRoom", parts[1]);
                        break;
                    case "join":
                        await connection.InvokeAsync("JoinRoom", parts[1]);
                        break;
                    case "send":
                        await connection.InvokeAsync("SendMessage", parts[1], parts.Length > 2 ? parts[2] : "");
                        break;
                    default:
                        Console.WriteLine("Unknown command.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        await connection.StopAsync();
        Console.WriteLine("Disconnected");
    }
}