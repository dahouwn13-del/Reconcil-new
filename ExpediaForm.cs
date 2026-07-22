using System.Text;
using CIEL.Reconciliation.Security;

namespace CIEL.Reconciliation;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();

        if (!AppServices.Users.HasUsers)
        {
            using var setup = new FirstRunSetupForm();
            if (setup.ShowDialog() != DialogResult.OK) return;
        }

        while (true)
        {
            CurrentSession.User = null;
            using var login = new LoginForm();
            if (login.ShowDialog() != DialogResult.OK) break;

            using var home = new HomeForm();
            Application.Run(home);
            if (!home.LogoutRequested) break;
        }
    }
}
