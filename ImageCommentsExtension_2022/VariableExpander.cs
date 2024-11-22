﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace ImageCommentsExtension_2022 {
    /// <summary>
    /// This class provides variable substitution for strings, e.g. replacing '$(ProjectDir)' with 'C:\MyProject\'
    /// Currently supported variables are:
    ///   $(ProjectDir)
    ///   $(SolutionDir)
    ///   $(ItemDir)
    /// </summary>
    public class VariableExpander
    {
        private readonly Regex _variableMatcher;
        private const string VARIABLE_PATTERN = @"\$\(\S+?\)";

        private const string PROJECTDIR_PATTERN = "$(ProjectDir)";
        private const string SOLUTIONDIR_PATTERN = "$(SolutionDir)";
        private const string ITEMDIR_PATTERN = "$(ItemDir)";

        private string _projectDirectory;
        private string _solutionDirectory;

        private readonly IWpfTextView _view;
        private readonly ITextDocument _textDoc=null;

        public VariableExpander(IWpfTextView view)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (view == null)
            {
                 throw new ArgumentNullException("view");
            }
            _view = view;
            _variableMatcher = new Regex(VARIABLE_PATTERN, RegexOptions.Compiled);

            try
            {
                populateVariableValues();
            }
            catch (Exception ex) // TODO [?]: Investigate exceptions that can be thrown with VS interop and refine the exception handling here
            {
                ExceptionHandler.Notify(ex, true);
            }
        }
        
        public VariableExpander(ITextDocument textDoc)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (textDoc == null)
            {
                 throw new ArgumentNullException("view");
            }
            _textDoc = textDoc;
            _variableMatcher = new Regex(VARIABLE_PATTERN, RegexOptions.Compiled);

            try
            {
                populateVariableValues();
            }
            catch (Exception ex) // TODO [?]: Investigate exceptions that can be thrown with VS interop and refine the exception handling here
            {
                ExceptionHandler.Notify(ex, true);
            }
        }
        
        /// <summary>
        /// Processes URL by replacing $(Variables) with their values
        /// </summary>
        /// <param name="urlString">Input URL string</param>
        /// <returns>Processed URL string</returns>
        public string ProcessText(string urlString)
        {
            string processedText = _variableMatcher.Replace(urlString, evaluator);
            return processedText;
        }

        /// <summary>
        /// Regex.Replace Match evaluator callback. Performs variable name/value substitution
        /// </summary>
        /// <param name="match"></param>
        /// <returns></returns>
        private string evaluator(Match match)
        {
            string variableName = match.Value;
            if (string.Compare(variableName, PROJECTDIR_PATTERN, StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                return _projectDirectory;
            }
            else if (string.Compare(variableName, SOLUTIONDIR_PATTERN, StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                return _solutionDirectory;
            }
            else if (string.Compare(variableName, ITEMDIR_PATTERN, StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                ITextDocument document=_textDoc;
                if (_view !=null && _view.TextDataModel.DocumentBuffer.Properties.TryGetProperty(typeof(ITextDocument), out document))
                {
                    return Path.GetDirectoryName(document.FilePath);
                }

                return variableName;
            }
            else
            {
                // Could throw an exception here, but it's possible the path contains $(...).
                // TODO: Variable name escaping
                return variableName;
            }
        }

        public string replacer(string path) {
            string res = null;
            res = Path.GetFullPath(path);
            if (path.StartsWith(_projectDirectory) == true) {
                res = path.Replace(_projectDirectory, PROJECTDIR_PATTERN+"");
            } else if (path.StartsWith(_solutionDirectory) == true) {
                res = path.Replace(_solutionDirectory, SOLUTIONDIR_PATTERN + "");
            } else {
                res = path;
            }
            return res;
        }

        /// <summary>
        /// Populates variable values from the ProjectItem associated with the TextView.
        /// </summary>
        /// <remarks>Based on code from http://stackoverflow.com/a/2493865
        /// Guarantees variables will not be null, but they may be empty if e.g. file isn't part of a project, or solution hasn't been saved yet
        /// TODO: If additional variables are added that reference the path to the document, handle cases of 'Save as' / renaming
        /// </remarks>
        private void populateVariableValues()
        {
            _projectDirectory = "";
            _solutionDirectory = "";
            
            ITextDocument document = _textDoc;
            if(document == null) { 
                _view.TextDataModel.DocumentBuffer.Properties.TryGetProperty(typeof (ITextDocument), out document);
            }
            var dte2 = (DTE2)Package.GetGlobalService(typeof (SDTE));
            ProjectItem projectItem = dte2.Solution.FindProjectItem(document.FilePath);
            
            if (projectItem != null && projectItem.ContainingProject != null)
            {
                string projectPath = projectItem.ContainingProject.FileName;
                if (projectPath != "") // projectPath will be empty if file isn't part of a project.
                {
                    _projectDirectory = Path.GetDirectoryName(projectPath) + @"\";
                }

                string solutionPath = dte2.Solution.FileName;
                if (solutionPath != "") // solutionPath will be empty if project isn't part of a saved solution
                {
                    _solutionDirectory = Path.GetDirectoryName(solutionPath) + @"\";
                }
            }
        }
    }
}
