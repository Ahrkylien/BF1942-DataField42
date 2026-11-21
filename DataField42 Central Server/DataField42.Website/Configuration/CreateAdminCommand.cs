using DataField42.Website.Services.Admin;

namespace DataField42.Website.Configuration;

/// <summary>
/// Creates or re-passwords an admin account from the command line, so the first account can be
/// made without a bootstrap page and a forgotten password can be reset with shell access.
/// </summary>
public static class CreateAdminCommand
{
    public const string Name = "create-admin";

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine($"usage: {Name} <username> <password>");
            return 1;
        }

        string userName = args[0];
        string password = args[1];

        if (userName.Length == 0)
        {
            Console.Error.WriteLine("The username may not be empty.");
            return 1;
        }

        if (password.Length == 0)
        {
            Console.Error.WriteLine("The password may not be empty.");
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Data.DataField42DbContext>();
        await Data.AdminSchema.EnsureCreatedAsync(context);

        var users = scope.ServiceProvider.GetRequiredService<AdminUserService>();

        if (await users.CreateAsync(userName, password) is not null)
        {
            Console.WriteLine($"Created admin account '{userName}'.");
            return 0;
        }

        if (await users.ChangePasswordAsync(userName, password))
        {
            Console.WriteLine($"Updated the password for admin account '{userName}'. Existing sessions were signed out.");
            return 0;
        }

        Console.Error.WriteLine($"Could not create or update admin account '{userName}'.");
        return 1;
    }
}
