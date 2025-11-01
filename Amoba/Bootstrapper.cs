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

            // ===================================================================
            // 1. SERVICES ÉS CORE KOMPONENSEK REGISZTRÁLÁSA
            // ===================================================================

            // A ViewService regisztrálása (megvolt)
            builder.RegisterType<ViewService>()
                   .As<IViewService>()
                   .WithParameter("rootFrame", rootFrame)
                   .SingleInstance();

            // --- HOZZÁADVA: Az INetworkService regisztrálása ---
            // Megmondjuk az Autofac-nak, hogy ha INetworkService kell,
            // akkor a NetworkService osztályból hozzon létre egyetlen példányt.
            builder.RegisterType<NetworkService>()
                   .As<INetworkService>()
                   .SingleInstance();
            // --- HOZZÁADÁS VÉGE ---


            // ===================================================================
            // 2. VIEWMODEL-EK REGISZTRÁLÁSA
            // ===================================================================
            var currentAssembly = typeof(Bootstrapper).GetTypeInfo().Assembly;
            // 1. Regisztrálja az összes "egyszerű" ViewModel-t, 
            //    aminek NINCS szüksége futásidejű paraméterre (pl. MainViewModel).
            builder.RegisterTypes(currentAssembly.GetTypes()
                   .Where(z => z.GetTypeInfo().BaseType == typeof(ViewModelBase) &&
                               z.Name != "GameViewModel" && // KIVÉTEL: GameViewModel
                               z.Name != "GameSizeViewModel" // KIVÉTEL: GameSizeViewModel
                   ).ToArray());

            // 2. Regisztrálja a "komplex" ViewModel-eket (amelyek paramétereket várnak).
            //    Az .AsSelf() megmondja az Autofac-nak, hogy "ez az osztály létezik",
            //    de ne próbálja meg felépíteni, amíg a ViewService nem kéri
            //    a futásidejű paraméterekkel.
            builder.RegisterType<GameViewModel>().AsSelf();
            builder.RegisterType<GameSizeViewModel>().AsSelf();
            // -------------------------

            var container = builder.Build();

            // ===================================================================
            // 3. VIEW/PAGE REGISZTRÁCIÓK (ViewModel -> View leképezés)
            // ===================================================================
            // Ez a rész a már felépült konténerből kéri el a ViewService-t,
            // és beállítja a ViewModel-View párokat a navigációhoz.
            var viewService = container.Resolve<IViewService>();

            viewService.RegisterPage(typeof(MainViewModel), typeof(MainPage));
            viewService.RegisterPage(typeof(GameSizeViewModel), typeof(GameSizePage));
            viewService.RegisterPage(typeof(GameViewModel), typeof(GamePage));

            return container;
        }
    }
}