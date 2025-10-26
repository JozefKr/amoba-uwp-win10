using Autofac;
using GalaSoft.MvvmLight;
using System;

namespace Amoba.Services
{
    // A ViewService által hivatkozott interfész frissítése
    public interface IViewService
    {
        void RegisterPage(Type vm, Type page);

        void OpenPage<T>(params NamedParameter[] parameters) where T : ViewModelBase;

        void OpenDialog<T>(params NamedParameter[] parameters) where T : ViewModelBase;
    }
}
