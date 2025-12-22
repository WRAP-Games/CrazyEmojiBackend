using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

// Data models matching server responses
public class CurrentUserData
{
    public string username { get; set; }
    public string roomCode { get; set; }
}

public class JoinedRoomData
{
    public string roomName { get; set; }
    public string category { get; set; }
    public int rounds { get; set; }
    public int roundDuration { get; set; }
    public string roomCreator { get; set; }
    public List<string> players { get; set; }
}

public class RoundResult
{
    public string username { get; set; }
    public bool guessedRight { get; set; }
    public string guessedWord { get; set; }
    public long gameScore { get; set; }
}

public static class RoomTest
{
    public static async Task RunAsync()
    {
        Console.WriteLine("Starting Room Test Client...");

        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5026/roomHub")
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .WithAutomaticReconnect()
            .Build();

        // Register all event handlers with proper types
        connection.On<string>("createdUser", username =>
            Console.WriteLine($"✓ User created: {username}"));

        connection.On("userLoggedIn", () =>
            Console.WriteLine($"✓ User logged in"));

        connection.On<CurrentUserData>("currentUserData", data =>
            Console.WriteLine($"✓ Current user: {data.username}, Room: {data.roomCode}"));

        connection.On<string>("userData", username =>
            Console.WriteLine($"✓ User data retrieved: {username}"));

        connection.On<string>("createdRoom", code =>
            Console.WriteLine($"✓ Room created with code: {code}"));

        connection.On<JoinedRoomData>("joinedRoom", data =>
        {
            Console.WriteLine($"✓ Joined room: {data.roomName}");
            Console.WriteLine($"  Category: {data.category}");
            Console.WriteLine($"  Rounds: {data.rounds}, Duration: {data.roundDuration}s");
            Console.WriteLine($"  Creator: {data.roomCreator}");
            Console.WriteLine($"  Players ({data.players.Count}): {string.Join(", ", data.players)}");
        });

        connection.On<string>("playerJoined", username =>
            Console.WriteLine($"✓ Player joined: {username}"));

        connection.On<string>("playerLeft", username =>
            Console.WriteLine($"✓ Player left: {username}"));

        connection.On("gameStarted", () =>
            Console.WriteLine($"✓ Game started!"));

        connection.On("gameEnded", () =>
            Console.WriteLine($"✓ Game ended!"));

        connection.On<string>("commanderSelected", commander =>
            Console.WriteLine($"✓ Commander selected: {commander}"));

        connection.On<string>("recivedWord", word =>
            Console.WriteLine($"✓ [COMMANDER] Your word is: {word}"));

        connection.On("emojisRecieved", () =>
            Console.WriteLine($"✓ Emojis sent successfully"));

        connection.On<List<string>>("recieveEmojis", emojis =>
            Console.WriteLine($"✓ [EMOJIS RECEIVED] {string.Join(" ", emojis)}"));

        connection.On<bool>("wordChecked", isCorrect =>
            Console.WriteLine($"✓ Word check: {(isCorrect ? "CORRECT! ✓" : "Incorrect ✗")}"));

       connection.On<List<RoundResult>>("roundEnded", results =>
        {
            Console.WriteLine("\n═══════════════════════════════════════════");
            Console.WriteLine("           ROUND ENDED - RESULTS            ");
            Console.WriteLine("═══════════════════════════════════════════");
            foreach (var result in results)
            {
                var status = result.guessedRight ? "✓ CORRECT" : "✗ Wrong";
                Console.WriteLine($"  {result.username,-15} | {status,-12} | Score: {result.gameScore}");
            }
            Console.WriteLine("═══════════════════════════════════════════\n");
        });

        connection.On("roundStarted", () =>
            Console.WriteLine($"\n✓ Next round started! Get ready...\n"));

        connection.On<string>("Error", error =>
            Console.WriteLine($"✗ [ERROR] {error}"));

        // Connection lifecycle events
        connection.Closed += async (error) =>
        {
            Console.WriteLine($"✗ Connection closed: {error?.Message}");
            await Task.CompletedTask;
        };

        connection.Reconnecting += error =>
        {
            Console.WriteLine($"⚠ Reconnecting: {error?.Message}");
            return Task.CompletedTask;
        };

        connection.Reconnected += connectionId =>
        {
            Console.WriteLine($"✓ Reconnected with ID: {connectionId}");
            return Task.CompletedTask;
        };

        try
        {
            await connection.StartAsync();
            Console.WriteLine("✓ Connected to server!");
            Console.WriteLine($"Connection ID: {connection.ConnectionId}\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to connect: {ex.Message}");
            return;
        }

        DisplayMenu();

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            var parts = input.Split(' ', 2);
            var command = parts[0].ToLower();

            try
            {
                switch (command)
                {
                    case "1":
                    case "create":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: create <USERNAME> <PASSWORD>");
                            Console.WriteLine("  Username: 3-32 chars, alphanumeric + underscore");
                            Console.WriteLine("  Password: 8-32 chars, alphanumeric + @$!%*?&_-");
                            break;
                        }
                        var userParts = parts[1].Split(' ');
                        if (userParts.Length < 2)
                        {
                            Console.WriteLine("Usage: create <USERNAME> <PASSWORD>");
                            break;
                        }
                        await connection.InvokeAsync("CreateUser", userParts[0], userParts[1]);
                        break;

                    case "2":
                    case "login":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: login <USERNAME> <PASSWORD>");
                            break;
                        }
                        var loginParts = parts[1].Split(' ');
                        if (loginParts.Length < 2)
                        {
                            Console.WriteLine("Usage: login <USERNAME> <PASSWORD>");
                            break;
                        }
                        await connection.InvokeAsync("LoginUser", loginParts[0], loginParts[1]);
                        break;

                    case "3":
                    case "current":
                        await connection.InvokeAsync("GetCurrentUserData");
                        break;

                    case "4":
                    case "getuser":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: getuser <USERNAME>");
                            break;
                        }
                        await connection.InvokeAsync("GetUserData", parts[1]);
                        break;

                    case "5":
                    case "createroom":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: createroom <ROOMNAME> <CATEGORY> <ROUNDS> <DURATION>");
                            Console.WriteLine("  Room name: 3-32 chars, alphanumeric + space + underscore");
                            Console.WriteLine("  Category: Valid category from database (e.g., General, Sports, Movies)");
                            Console.WriteLine("  Rounds: 10-30");
                            Console.WriteLine("  Duration: 15-45 seconds");
                            break;
                        }
                        var roomParts = parts[1].Split(' ');
                        if (roomParts.Length < 4)
                        {
                            Console.WriteLine("Usage: createroom <ROOMNAME> <CATEGORY> <ROUNDS> <DURATION>");
                            break;
                        }
                        if (!int.TryParse(roomParts[2], out int rounds) || !int.TryParse(roomParts[3], out int duration))
                        {
                            Console.WriteLine("✗ Rounds and Duration must be numbers");
                            break;
                        }
                        await connection.InvokeAsync("CreateRoom", roomParts[0], roomParts[1], rounds, duration);
                        break;

                    case "6":
                    case "join":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: join <ROOMCODE>");
                            Console.WriteLine("  Room code: 6-character alphanumeric code");
                            break;
                        }
                        await connection.InvokeAsync("JoinRoom", parts[1].Trim().ToUpper());
                        break;

                    case "7":
                    case "left":
                    case "leave":
                        await connection.InvokeAsync("LeftRoom");
                        break;

                    case "8":
                    case "start":
                        await connection.InvokeAsync("StartGame");
                        break;

                    case "9":
                    case "commander":
                        await connection.InvokeAsync("GetCommander");
                        break;

                    case "10":
                    case "word":
                        await connection.InvokeAsync("GetWord");
                        break;

                    case "11":
                    case "emojis":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: emojis <EMOJI1> <EMOJI2> ...");
                            Console.WriteLine("  Example: emojis 😀 🎮 🎉");
                            break;
                        }
                        var emojiList = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                        await connection.InvokeAsync("SendEmojis", emojiList);
                        break;

                    case "12":
                    case "guess":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: guess <WORD>");
                            break;
                        }
                        await connection.InvokeAsync("CheckWord", parts[1].Trim());
                        break;

                    case "13":
                    case "results":
                        await connection.InvokeAsync("GetResults");
                        break;

                    case "help":
                    case "h":
                    case "?":
                        DisplayMenu();
                        break;

                    case "clear":
                    case "cls":
                        Console.Clear();
                        DisplayMenu();
                        break;

                    case "exit":
                    case "quit":
                    case "q":
                        break;

                    default:
                        Console.WriteLine("✗ Unknown command. Type 'help' for commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
            }
        }

        await connection.StopAsync();
        Console.WriteLine("\n✓ Disconnected from server. Goodbye!");
    }

    private static void DisplayMenu()
    {
        Console.WriteLine("\n═══════════════════════════════════════════");
        Console.WriteLine("          CRAZY EMOJI - CONSOLE TEST        ");
        Console.WriteLine("═══════════════════════════════════════════\n");
        Console.WriteLine("USER COMMANDS:");
        Console.WriteLine("  1.  create <USERNAME> <PASSWORD>     - Create new user");
        Console.WriteLine("  2.  login <USERNAME> <PASSWORD>      - Login user");
        Console.WriteLine("  3.  current                          - Get current user data");
        Console.WriteLine("  4.  getuser <USERNAME>               - Get user data");
        Console.WriteLine("\nROOM COMMANDS:");
        Console.WriteLine("  5.  createroom <NAME> <CAT> <R> <D>  - Create room");
        Console.WriteLine("                                         (Rounds: 10-30, Duration: 15-45s)");
        Console.WriteLine("  6.  join <ROOMCODE>                  - Join a room");
        Console.WriteLine("  7.  left                             - Leave room");
        Console.WriteLine("\nGAME COMMANDS:");
        Console.WriteLine("  8.  start                            - Start game (min 3 players, room creator only)");
        Console.WriteLine("  9.  commander                        - Select commander for round");
        Console.WriteLine("  10. word                             - Get word (commander only)");
        Console.WriteLine("  11. emojis <E1> <E2> ...             - Send emojis (commander only)");
        Console.WriteLine("  12. guess <WORD>                     - Guess the word (players only)");
        Console.WriteLine("  13. results                          - Get round results");
        Console.WriteLine("\nOTHER:");
        Console.WriteLine("  help, h, ?                           - Show this menu");
        Console.WriteLine("  clear, cls                           - Clear screen");
        Console.WriteLine("  exit, quit, q                        - Disconnect and exit");
        Console.WriteLine("═══════════════════════════════════════════\n");
    }
}