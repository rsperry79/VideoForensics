using VideoForensics.Utils.DbRepair.Contracts;

namespace VideoForensics.Utils.DbRepair;

/// <summary>
/// Implements password prompting via the terminal with hidden input (no echo).
/// </summary>
public class TerminalPasswordPrompt : IPasswordPrompt
{
    public async Task<string> PromptPasswordAsync(CancellationToken ct)
    {
        return ReadPasswordHidden("Password: ");
    }

    public async Task<string> PromptConfirmationAsync(CancellationToken ct)
    {
        return ReadPasswordHidden("Confirm password: ");
    }

    public async Task<bool> AskConfirmationAsync(string prompt, CancellationToken ct)
    {
        Console.Write(prompt);
        string? response = Console.ReadLine();
        return response?.ToLower() == "y";
    }

    private static string ReadPasswordHidden(string prompt)
    {
        Console.Write(prompt);
        var password = new System.Text.StringBuilder();

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }

        return password.ToString();
    }
}
