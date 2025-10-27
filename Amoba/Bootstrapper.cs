using Amoba.Services;
using Amoba.ViewModel;
using Amoba.Views;
using Autofac;
using GalaSoft.MvvmLight;
using System.Linq;
using System.Reflection;
using Windows.UI.Xaml.Controls;

namespace Amoba
{
    public static class Bootstrapper
    {
        public static IContainer Bootstrap(Frame rootFrame)
        {
            var builder = new ContainerBuilder();

            // 1. SERVICES ÉS CORE KOMPONENSEK REGISZTRÁLÁSA
            builder.RegisterType<ViewService>()
                   .As<IViewService>()
                   .WithParameter("rootFrame", rootFrame)
                   .SingleInstance();
     
            // 2. VIEWMODEL-EK REGISZTRÁLÁSA
            // Regisztrálja az összes ViewModelBase-ből származó típust (beleértve a MainViewModel-t is)
            var currentAssembly = typeof(Bootstrapper).GetTypeInfo().Assembly;

            // Regisztrálja az ÖSSZES ViewModelBase-ből származó típust.
            //builder.RegisterTypes(currentAssembly.GetTypes().Where(z => z.GetTypeInfo().BaseType == typeof(ViewModelBase) && z.Name != "GameViewModel").ToArray());
            //builder.RegisterType<GameViewModel>().WithParameter(new NamedParameter("boardSizeParam", "size"));
            builder.RegisterTypes(currentAssembly.GetTypes().Where(z => z.GetTypeInfo().BaseType == typeof(ViewModelBase)).ToArray());

            var container = builder.Build();

            // 3. VIEW/PAGE REGISZTRÁCIÓK (ViewModel -> View leképezés)
            var viewService = container.Resolve<IViewService>();

            viewService.RegisterPage(typeof(MainViewModel), typeof(MainPage));
            viewService.RegisterPage(typeof(GameSizeViewModel), typeof(GameSizePage));
            viewService.RegisterPage(typeof(GameViewModel), typeof(GamePage));

            return container;
        }
    }
}
