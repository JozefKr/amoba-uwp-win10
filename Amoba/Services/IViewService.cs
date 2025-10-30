using Autofac.Core;
using GalaSoft.MvvmLight;
using System;

namespace Amoba.Services
{
    // A ViewService által hivatkozott interfész frissítése
    public interface IViewService
    {
        void RegisterPage(Type vm, Type page);

        void OpenPage<TViewModel>(Parameter[] parameters) where TViewModel : ViewModelBase;
    }
}
