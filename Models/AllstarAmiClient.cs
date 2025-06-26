using AsteriskDataStream.Models;
using AsteriskDataStream.Services;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

public class AllstarAmiClient
{
    private TcpClient tcpClient;
    private NetworkStream? stream;
    private StreamWriter? writer;
    private StreamReader? reader;
    private CancellationTokenSource cancellationTokenSource;
    private readonly string amiHost;
    private readonly int amiPort;
    private readonly string amiUsername;
    private readonly string amiPassword;
    private Task? readerTask;
    private BlockingCollection<string> responseQueue = new(); // Optional: use ConcurrentQueue if preferred
    private readonly SemaphoreSlim _streamLock = new(1, 1);
    private int _actionId = 0; // Action ID for the AMI commands

    public readonly string NodeNumber;
    public List<AllstarConnection> AllstarConnections { get; set; }
    public string ActionID => $"{Interlocked.Increment(ref _actionId)}"; // Thread-safe increment for action ID

    public AllstarAmiClient(string amiHost, int amiPort, string amiUsername, string amiPassword, string nodeNumber)
    {
        this.amiHost = amiHost;
        this.amiPort = amiPort;
        this.amiUsername = amiUsername;
        this.amiPassword = amiPassword;
        this.NodeNumber = nodeNumber;

        tcpClient = new TcpClient();

        AllstarConnections = new List<AllstarConnection>();
        cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task ConnectAsync()
    {
        if (tcpClient.Connected)
            return;

        ConsoleHelper.Write($"Connecting to AMI server at {amiHost}:{amiPort}", "* ", ConsoleColor.DarkYellow, ConsoleColor.Black, true);

        try
        {
            await tcpClient.ConnectAsync(amiHost, amiPort);
        }
        catch (Exception ex)
        {
            ConsoleHelper.Write($"Unable to connect to the AMI server ({amiHost}:{amiPort}).", "* ", ConsoleColor.Red, ConsoleColor.Black, true);
            ConsoleHelper.Write(ex.Message, "", ConsoleColor.Red, ConsoleColor.Black, true);
            return;
        }

        if (!tcpClient.Connected)
        {
            ConsoleHelper.Write("Unable to connect to the AMI server.", "* ", ConsoleColor.Red, ConsoleColor.Black, true);
            return;
        }

        ConsoleHelper.Write($"Connected to AMI server at {amiHost}:{amiPort}.", "* ", ConsoleColor.DarkYellow, ConsoleColor.Black, true);

        stream = tcpClient.GetStream();
        writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
        reader = new StreamReader(stream, Encoding.ASCII);

        // Write the login manually instead of using SendAsync()
        var loginCommand =
            $"ACTION: LOGIN\r\n" +
            $"USERNAME: {amiUsername}\r\n" +
            $"SECRET: {amiPassword}\r\n" +
            $"EVENTS: ON\r\n" +
            $"ActionID: {ActionID}";


        ConsoleHelper.Write(loginCommand, "", ConsoleColor.Blue, ConsoleColor.Black, true);

        await writer.WriteLineAsync(loginCommand);
        await writer.FlushAsync();  // <-- critical flush

        ConsoleHelper.Write("Login command sent to AMI server.", "* ", ConsoleColor.DarkYellow, ConsoleColor.Black, true);

        // Start reader loop after successful login
        readerTask = Task.Run(() => ReaderLoopAsync(cancellationTokenSource.Token));

    }

    private async Task SendAsync(string command)
    {
        await _streamLock.WaitAsync();

        try
        {
            if (tcpClient == null || !tcpClient.Connected)
            {
                ConsoleHelper.Write("TCP client is not connected. Attempting to connect...", "* ", ConsoleColor.DarkYellow, ConsoleColor.Black, true);
                await ConnectAsync();
            }

            if (tcpClient != null && tcpClient.Connected)
            {
                command = command.Trim();

                ConsoleHelper.Write(command, "", ConsoleColor.Blue, ConsoleColor.Black, true);

                await writer!.WriteLineAsync("\r\n" + command.Trim() + "\r\n\r\n");
                await writer.FlushAsync();
            }
        }
        finally
        {
            _streamLock.Release();
        }
    }

    private async Task ReaderLoopAsync(CancellationToken cancellationToken)
    {
        if (reader == null)
        {
            ConsoleHelper.Write("StreamReader is null. Cannot start reading responses.", "* ", ConsoleColor.Red, ConsoleColor.Black, true);
            return;
        }

        try
        {
            StringBuilder messageBuilder = new();
            while (!cancellationToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync();

                if (line == null)
                {
                    ConsoleHelper.Write("Stream closed by server.", "* ", ConsoleColor.Red, ConsoleColor.Black, true);
                    break;
                }
                else if (line == string.Empty)
                {
                    // Message boundary (blank line)
                    string fullMessage = messageBuilder.ToString();
                    messageBuilder.Clear();

                    // Optionally queue or dispatch the message
                    responseQueue.Add(fullMessage);

                    ParseResponse(fullMessage);
                }
                else
                {
                    messageBuilder.AppendLine(line);
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.Write($"Reader loop exception: {ex.GetType().Name}: {ex.Message}", "* ", ConsoleColor.Red, ConsoleColor.Black, true);
        }
    }

    public async Task GetNodeInfoAsync(string nodeNumber)
    {
        var rptCommand =
            $"ACTION: RptStatus\r\n" +
            $"COMMAND: XStat\r\n" +
            $"NODE: {nodeNumber}\r\n" + 
            $"ActionID: {ActionID}";

        await SendAsync(rptCommand);
    }

    private void ParseResponse(string rawMessage)
    {
        // Replace CRLF and bare CR with LF, then trim end
        var normalizedMessage = rawMessage.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd();

        // Parse the raw message into a structured format if needed
        if (string.IsNullOrEmpty(normalizedMessage))
        {
            return;
        }

        var welcomeRegex = new Regex(@"^Asterisk Call Manager/1.0\s*$", RegexOptions.Multiline);
        var errorRegex = new Regex(@"^Response: Error\r?\nMessage: (.*)$", RegexOptions.Multiline);
        var successRegex = new Regex(@"^Response: Success\r?\n(?:ActionID: .*\r?\n)?Message: (.*)$", RegexOptions.Multiline);
        var xStatRegex = new Regex(@"
^(?:ActionID:\s(.*)\n)?
Response:\sSuccess\n
Node:\s(.*)\n
(?:(?:Conn:\s.*\n)+)?
(?:LinkedNodes:\s(.*))?\n
(?:Var:\sRPT_TXKEYED=(.*)\n)?
(?:Var:\sRPT_NUMLINKS=(.*)\n)?
(?:Var:\sRPT_LINKS=(.*)\n)?
(?:Var:\sRPT_NUMALINKS=(.*)\n)?
(?:Var:\sRPT_ALINKS=(.*)\n)?
(?:Var:\sRPT_RXKEYED=(.*)\n)?
(?:Var:\sRPT_AUTOPATCHUP=(.*)\n)?
(?:Var:\sRPT_ETXKEYED=(.*)\n)?
(?:Var:\sTRANSFERCAPABILITY=(.*)\n)?
(?:parrot_ena:\s(.*)\n)?
(?:sys_ena:\s(.*)\n)?
(?:tot_ena:\s(.*)\n)?
(?:link_ena:\s(.*)\n)?
(?:patch_ena:\s(.*)\n)?
(?:patch_state:\s(.*)\n)?
(?:sch_ena:\s(.*)\n)?
(?:user_funs:\s(.*)\n)?
(?:tail_type:\s(.*)\n)?
(?:iconns:\s(.*)\n)?
(?:tot_state:\s(.*)\n)?
(?:ider_state:\s(.*)\n)?
(?:tel_mode:\s(.*)\n)?
",
RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);

        var rptLinksRegex = new Regex(@"^Event: (?:RPT_ALINKS|RPT_NUMALINKS|RPT_LINKS|RPT_NUMLINKS|RPT_TXKEYED)\r?\n(?:Privilege: (.*)\r?\n)(?:Node: (.*)\r?\n)(?:Channel: (.*)\r?\n)(?:EventValue: (.*)\r?\n)(?:LastKeyedTime: (.*)\r?\n)\r?\n(?:LastTxKeyedTime: (.*)\n)", RegexOptions.Multiline);
        var newChannelRegex = new Regex(@"^Event: Newchannel\r?\n(?:Privilege: (.*)\r?\n)(?:Channel: (.*)\r?\n)(?:State: (.*)\r?\n)(?:CallerIDNum: (.*)\r?\n)(?:CallerIDName: (.*)\r?\n)(?:Uniqueid: (.*)\r?\n)", RegexOptions.Multiline);
        var hangupRegex = new Regex(@"^Event: Hangup\r?\n(?:Privilege: (.*)\r?\n)(?:Channel: (.*)\r?\n)(?:Uniqueid: (.*)\r?\n)(?:Cause: (.*)\r?\n)(?:Cause-txt: (.*)\r?\n)", RegexOptions.Multiline);
        List<Regex> regexList = new List<Regex>
        {
            welcomeRegex,
            errorRegex,
            successRegex,
            xStatRegex,
            rptLinksRegex,
            newChannelRegex,
            hangupRegex
        };
        var connLineRegex = new Regex(@"^Conn:\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)$", RegexOptions.Multiline);


        foreach (var regex in regexList)
        {
            var match = regex.Match(normalizedMessage);
            if (match.Success)
            {
                // Handle the matched response
                switch (regex)
                {
                    case var _ when regex == welcomeRegex:
                        ConsoleHelper.Write(rawMessage, "", ConsoleColor.Green, ConsoleColor.Black, true);
                        return;
                    case var _ when regex == errorRegex:
                        if (match.Groups[1].Value != "Missing action in request")
                            ConsoleHelper.Write($"Error: {match.Groups[1].Value}", "", ConsoleColor.Red, ConsoleColor.Black, true);
                        return;
                    case var _ when regex == successRegex:
                        ConsoleHelper.Write($"Success: {match.Groups[1].Value}", "* ", ConsoleColor.Green, ConsoleColor.Black, true);
                        return;
                    case var _ when regex == xStatRegex:
                        // Populate connection metadata if needed later

                        // Process direct connections
                        var connMatches = connLineRegex.Matches(normalizedMessage);
                        foreach (Match cm in connMatches)
                        {
                            var connection = AllstarConnection.FindOrCreate(cm.Groups[1].Value, AllstarConnections);
                            connection.IpAddress = cm.Groups[2].Value;
                            connection.SomeNumber = cm.Groups[3].Value;
                            connection.Direction = cm.Groups[4].Value;
                            connection.TimeSpanConnected = cm.Groups[5].Value;
                            connection.Status = cm.Groups[6].Value;
                            connection.Type = "Direct";
                        }

                        // Process linked nodes
                        var arrLinkedNodes = match.Groups[3].Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var node in arrLinkedNodes)
                        {
                            var connection = AllstarConnection.FindOrCreate(node.Trim().Substring(1), AllstarConnections);
                            connection.Node = node.Trim().Substring(1);
                            connection.Type = "Linked";
                            connection.Status = node.Trim().Substring(0, 1) == "R" ? "Monitoring" : "Established";
                        }

                        ConsoleHelper.Write(rawMessage, "", ConsoleColor.Green, ConsoleColor.Black, true);
                        return;
                    case var _ when regex == rptLinksRegex:
                        // Handle RPT_LINKS event
                        ConsoleHelper.Write("Handle RPT_LINKS event", "", ConsoleColor.Gray, ConsoleColor.Black, true);
                        break;
                    case var _ when regex == newChannelRegex:
                        // Handle Newchannel event
                        ConsoleHelper.Write("Handle Newchannel event", "", ConsoleColor.Gray, ConsoleColor.Black, true);
                        break;
                    case var _ when regex == hangupRegex:
                        // Handle Hangup event
                        ConsoleHelper.Write("Handle Hangup event", "", ConsoleColor.Gray, ConsoleColor.Black, true);
                        break;
                }
            }
        }

        // Display non-visible characters
        rawMessage = string.Concat(normalizedMessage.Select(c => char.IsControl(c) ? $"\\x{(int)c:X2}" : c.ToString()));

        ConsoleHelper.Write($"{Environment.NewLine}** No match for raw message ***********{Environment.NewLine}{Environment.NewLine}{normalizedMessage}{Environment.NewLine}", "", ConsoleColor.Yellow, ConsoleColor.Black, true);

        //var regex = new Regex(@"(?<=^|\r\n)(Response|ActionID|Message|Node|Conn|LinkedNodes|RPT_LINKS):\s*(.*?)\r?\n", RegexOptions.Multiline);
        //foreach (Match match in regex.Matches(rawMessage))
        //{
        //    var key = match.Groups[1].Value;
        //    var value = match.Groups[2].Value;

        //    switch (key)
        //    {
        //        case "Conn":
        //            try
        //            {
        //                // Check to see if this is a SawStat result
        //                string sawStatPattern = @"^(\d+)\s(\d+)\s(\d+)\s(\d+)$";
        //                var sawStatMatch = Regex.Match(value, sawStatPattern);
        //                if (sawStatMatch.Success) // SawStat result
        //                {
        //                    // Parse as SawStat result
        //                    var connection = AllstarConnection.FindOrCreate(sawStatMatch.Groups[1].Value, AllstarConnections);
        //                    connection.Transmitting = sawStatMatch.Groups[2].Value == "1";
        //                    connection.TimeSinceTransmit = sawStatMatch.Groups[3].Value;
        //                    connection.Type = "Direct";
        //                }
        //                else // XSTAT result
        //                {

        //                }
        //            }
        //            catch (Exception)
        //            {
        //                ConsoleHelper.Write("Error parsing connection data:\r\n" + value, "** ", ConsoleColor.Red);
        //            }

        //            break;
        //        case "LinkedNodes":
        //            var arrLinkedNodes = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        //            foreach (var node in arrLinkedNodes)
        //            {
        //                var connection = AllstarConnection.FindOrCreate(node.Trim().Substring(1), AllstarConnections);
        //                connection.Node = node.Trim().Substring(1);
        //                connection.Type = "Linked";
        //                connection.Status = node.Trim().Substring(0, 1) == "R" ? "Monitoring" : "Established";
        //            }
        //            break;
        //    }
        //}
    }

    public void ClearExpiredConnections(TimeSpan expirationTime)
    {
        var expiredConnections = AllstarConnections.Where(c => DateTime.UtcNow - c.LastHeardUtc > expirationTime).ToList();
        foreach (var connection in expiredConnections)
        {
            AllstarConnections.Remove(connection);
        }
    }
}

public class AllstarConnection
{
    public string Node { get; set; }
    public string? IpAddress { get; set; }
    public string? SomeNumber { get; set; }
    public string? Direction { get; set; }
    public string? TimeSpanConnected { get; set; }
    public DateTime LastHeardUtc { get; set; }
    public string? Status { get; set; }
    public string? Type { get; set; }
    public string? CallSign { get; set; }
    public string? Location { get; set; }
    public string? Info1 { get; set; }
    public string? Info2 { get; set; }
    public string? Latitude { get; set; }
    public string? Longitude { get; set; }
    public bool Transmitting { get; set; }
    public string? TimeSinceTransmit { get; set; }

    public void LoadMetadata()
    {
        int nodeNumber = 0;

        string callsignPattern = @"^[a-zA-Z0-9]{1,3}[0-9][a-zA-Z0-9]{0,3}[a-zA-Z]$";
        var callsignMatch = Regex.Match(Node, callsignPattern);

        if (callsignMatch.Success)
        {
            Info2 = "RepeaterPhone / Direct connection";
        }
        else if (int.TryParse(Node, out nodeNumber))
        {
            if (nodeNumber > 3000000)
            {
                // Echolink
                Info2 = "Echolink";
            }
            else if (nodeNumber < 2000)
            {
                Info2 = "Private node";
            }
            else
            {
                // Fetch metadata for the node
                var metadata = MetadataService.GetNodeMetadata(Node);
                if (metadata != null)
                {
                    CallSign = metadata.CallSign;
                    Location = metadata.Location;
                    Info1 = metadata.Info1;
                    Info2 = metadata.Info2;
                    Latitude = metadata.Latitude.ToString();
                    Longitude = metadata.Longitude.ToString();
                }
            }
        }
    }

    public static AllstarConnection FindOrCreate(string node, List<AllstarConnection> connections)
    {
        var connection = connections.FirstOrDefault(conn => conn.Node == node);

        if (connection == default) // This connection does not exist
        {
            connection = new AllstarConnection
            {
                Node = node
            };
            connection.LoadMetadata(); // Load metadata for the new connection
            connections.Add(connection);
        }

        connection.LastHeardUtc = DateTime.UtcNow; // Update last heard time

        return connection;
    }
}