using Newtonsoft.Json;

namespace AsteriskAMIStream.Models
{
    public class AMISettings
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string NodeNumber { get; set; }


        public static void PromptForConfiguration(string configFilePath) 
        {
            // If AMISettings is invalid or empty, prompt user for configuration
            Console.Clear();
            Console.WriteLine($"\r\n\r\nAMISettings section is missing or invalid in the configuration file. Please provide the information below. These details will be saved into {configFilePath} - update it as needed anytime.\r\n\r\n");

            var amiHost = PromptUser("Enter AMI Host");
            var amiPort = int.Parse(PromptUser("Enter AMI Port", "5038"));
            var amiUsername = PromptUser("Enter AMI Username", "admin");
            var amiPassword = PromptUser("Enter AMI Password");
            var nodeNumber = PromptUser("Enter Node Number");

            // Create the config object
            var configData = new
            {
                AMISettings = new[]
                {
            new
            {
                Host = amiHost,
                Port = amiPort,
                Username = amiUsername,
                Password = amiPassword,
                NodeNumber = nodeNumber
            }
        }
            };

            // Save the user input into the appsettings.json
            var json = JsonConvert.SerializeObject(configData, Formatting.Indented);
            File.WriteAllText(configFilePath, json);
            Console.WriteLine($"\r\n\r\nConfiguration saved to {configFilePath}.");
        }

        // Helper method for user input
        private static string PromptUser(string prompt, string defaultValue = "")
        {
            var defaultValueText = string.IsNullOrWhiteSpace(defaultValue) ? "" : $" (default: {defaultValue})";

            Console.Write($"{prompt}{defaultValueText}:\r\n  > ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }

            return input;
        }
    }

    public class Config
    {
        public List<AMISettings> AMISettings { get; set; }
    }
}
