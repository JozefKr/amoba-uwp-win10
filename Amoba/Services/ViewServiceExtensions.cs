using Autofac;
using GalaSoft.MvvmLight;

namespace Amoba.Services
{
    /// <summary>
    /// Kiterjesztő metódusok az IViewService interfészhez.
    /// </summary>
    public static class ViewServiceExtensions
    {
        /// <summary>
        /// Kiterjesztő metódus, amely lehetővé teszi az OpenPage hívását
        /// közvetlenül NamedParameter tömbbel a visszamenőleges kompatibilitás érdekében.
        /// </summary>
        public static void OpenPage<TViewModel>(this IViewService viewService, params NamedParameter[] parameters)
            where TViewModel : ViewModelBase
        {
            // Egyszerűen meghívja az interfész fő OpenPage metódusát.
            // A NamedParameter[] implicit módon konvertálható Parameter[] tömbbé.
            viewService.OpenPage<TViewModel>(parameters);
        }
    }
}
