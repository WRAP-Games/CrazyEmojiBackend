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

        // Register all event handlers
        connection.On<string>("UsernameSet", username => Console.WriteLine($"Username set: {username}"));
        connection.On<string>("CreatedRoom", code => Console.WriteLine($"Room created: {code}"));
        connection.On<string>("JoinedRoom", code => Console.WriteLine($"Joined room: {code}"));
        connection.On<string>("GameStarted", code => Console.WriteLine($"Game started in room: {code}"));
        connection.On<string>("PlayerLeft", connectionId => Console.WriteLine($"Player left: {connectionId}"));
        connection.On<string>("CommanderSelected", message => Console.WriteLine($"[COMMANDER] {message}"));
        connection.On<string>("CommanderAnnounced", message => Console.WriteLine($"[ANNOUNCEMENT] {message}"));
        connection.On<string>("ReceiveWord", word => Console.WriteLine($"[COMMANDER] Your word is: {word}"));
        connection.On<string>("ReceiveEmojis", emojis => Console.WriteLine($"[EMOJIS RECEIVED] {emojis}"));
        connection.On<string>("CorrectGuess", message => Console.WriteLine($"[CORRECT] {message}"));
        connection.On<string, int>("CorrectGuess", (msg, points) => Console.WriteLine($"[CORRECT] {msg} (+{points} points)"));
        connection.On<string>("IncorrectGuess", message => Console.WriteLine($"[INCORRECT] {message}"));
        connection.On<string, int>("IncorrectGuess", (msg, points) => Console.WriteLine($"[INCORRECT] {msg} (+{points} points)"));
        connection.On<string>("AllGuessedRight", message => Console.WriteLine($"[RESULT] {message}"));
        connection.On<string>("AllGuessedWrong", message => Console.WriteLine($"[RESULT] {message}"));
        connection.On<string, int>("CommanderBonus", (msg, points) => Console.WriteLine($"[COMMANDER] {msg} (+{points} points)"));
        connection.On<string>("RoundEnded", message => Console.WriteLine($"[ROUND] {message}"));
        connection.On<string>("Error", error => Console.WriteLine($"[ERROR] {error}"));

        await connection.StartAsync();

        Console.WriteLine("Connected!");

        Console.WriteLine("Type a command:");
        Console.WriteLine("1. setname <USERNAME>");
        Console.WriteLine("2. create <ROOMCODE>");
        Console.WriteLine("3. join <ROOMCODE>");
        Console.WriteLine("4. start");
        Console.WriteLine("5. emojis <EMOJI SEQUENCE>");
        Console.WriteLine("6. guess <WORD>");
        Console.WriteLine("7. exit");

        while (true)
        {
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            var parts = input.Split(' ', 2);
            var command = parts[0].ToLower();

            try
            {
                switch (command)
                {
                    case "setname":
                        if (parts.Length < 2) { Console.WriteLine("Usage: setname <USERNAME>"); break; }
                        await connection.InvokeAsync("SetUsername", parts[1]);
                        break;
                    case "create":
                        if (parts.Length < 2) { Console.WriteLine("Usage: create <ROOMCODE>"); break; }
                        await connection.InvokeAsync("CreateRoom", parts[1]);
                        break;
                    case "join":
                        if (parts.Length < 2) { Console.WriteLine("Usage: join <ROOMCODE>"); break; }
                        await connection.InvokeAsync("JoinRoom", parts[1]);
                        break;
                    case "start":
                        await connection.InvokeAsync("StartGame");
                        break;
                    case "emojis":
                        if (parts.Length < 2) { Console.WriteLine("Usage: emojis <EMOJI SEQUENCE>"); break; }
                        await connection.InvokeAsync("GetAndSendEmojis", parts[1]);
                        break;
                    case "guess":
                        if (parts.Length < 2) { Console.WriteLine("Usage: guess <WORD>"); break; }
                        await connection.InvokeAsync("CheckWord", parts[1]);
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