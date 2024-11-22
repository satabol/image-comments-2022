using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ImageCommentsExtension_2022 {
    class DependencyAssemblyLoader {

        static DependencyAssemblyLoader() {
            Main();
        }

        /**
         * Методы для запуска приложения после сборки в один файл:
         */
        static Dictionary<string, Assembly> assembliesDictionary = new Dictionary<string, Assembly>();

        static bool handler_registered=false;

        [STAThread]
        public static void Main() {
            if (handler_registered == false) {
                handler_registered = true;
                AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;
            }
        }

        private static Assembly OnResolveAssembly(object sender, ResolveEventArgs args) {
            try {
                Debug.WriteLine($"{nameof(ImageCommentsExtension_2022)}. Try load {args.Name}");
                AssemblyName assemblyName = new AssemblyName(args.Name);
                Assembly executingAssembly = Assembly.GetExecutingAssembly();
                string path = string.Format("{0}.dll", assemblyName.Name);

                if (assemblyName.CultureInfo != null && assemblyName.CultureInfo.Equals(CultureInfo.InvariantCulture) == false) {
                    path = String.Format(@"{0}\{1}", assemblyName.CultureInfo, path);
                }

                if (!assembliesDictionary.ContainsKey(path)) {
                    using (Stream assemblyStream = executingAssembly.GetManifestResourceStream(path)) {
                        if (assemblyStream != null) {
                            byte[] assemblyRawBytes = new byte[assemblyStream.Length];
                            assemblyStream.Read(assemblyRawBytes, 0, assemblyRawBytes.Length);
                            using (Stream pdbStream = executingAssembly.GetManifestResourceStream(Path.ChangeExtension(path, "pdb"))) {
                                if (pdbStream != null) {
                                    // Надо разобраться, что делает этот блок: ???
                                    byte[] pdbData = new Byte[pdbStream.Length];
                                    pdbStream.Read(pdbData, 0, pdbData.Length);
                                    Assembly assembly_pdb = Assembly.Load(assemblyRawBytes, pdbData);
                                    assembliesDictionary.Add(path, assembly_pdb);
                                    return assembly_pdb;
                                }
                            }
                            {
                                Assembly assembly_raw = Assembly.Load(assemblyRawBytes);
                                assembliesDictionary.Add(path, assembly_raw);
                            }
                        } else {
                            // Попытаться найти ресурсы в зависимом классе:
                            //tryResolvExternal(sender, args);
                            // Если загрузки не произошло, то протисать в этот модуль null:
                            if (assembliesDictionary.ContainsKey(path) == false) {
                                assembliesDictionary.Add(path, null);
                            }
                        }
                    }
                }
                return assembliesDictionary[path];
            } catch (System.Exception _ex) {
                //MessageBox.Show($"Ошибка при загрузке модуля {_ex.ToString()}");
                string str_error = $"{nameof(ImageCommentsExtension_2022)}. WARNING 0001. Не удалось прочитать ресурс {args.Name}.";
                Console.WriteLine(str_error);
                Debug.WriteLine(str_error);
                throw _ex;
            }
        }
    }
}
