using System;
using System.Windows;
using VideoOS.Platform.SDK.UI.LoginDialog;

namespace MultiUserEnvironment
{

    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            VideoOS.Platform.SDK.Media.Environment.Initialize();
        }
    }
}
