using Autofac;
using Autofac.Core;
using GalaSoft.MvvmLight;
using System;

namespace Amoba.Services
{
    // A ViewService által hivatkozott interfész frissítése
    public interface IViewService
    {
        void RegisterPage(Type vm, Type page);

        void OpenPage<T>(params NamedParameter[] parameters) where T : ViewModelBase;
        //void OpenPage<TViewModel>(params Parameter[] parameters) where TViewModel : ViewModelBase;
        // VAGY
        //void OpenPage<TViewModel>(Parameter parameter) where TViewModel : ViewModelBase;
    }
}
