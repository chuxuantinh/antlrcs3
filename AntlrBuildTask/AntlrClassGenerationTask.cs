/*
 * [The "BSD licence"]
 * Copyright (c) 2009 Sam Harwell
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * 3. The name of the author may not be used to endorse or promote products
 *    derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
 * THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

namespace Antlr3.Build.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Directory = System.IO.Directory;
    using File = System.IO.File;
    using FileAttributes = System.IO.FileAttributes;
    using Path = System.IO.Path;
#if !NETSTANDARD
    using System.Diagnostics;
#endif

    public class AntlrClassGenerationTask
        : Task
    {
        private static readonly SemaphoreSlim _lock = new SemaphoreSlim(1);

        private const string DefaultGeneratedSourceExtension = "g";
        private List<ITaskItem> _generatedCodeFiles = new List<ITaskItem>();

        public AntlrClassGenerationTask()
        {
            this.GeneratedSourceExtension = DefaultGeneratedSourceExtension;
        }

        [Required]
        public string AntlrToolPath
        {
            get;
            set;
        }

        [Required]
        public string OutputPath
        {
            get;
            set;
        }

        public string TargetLanguage
        {
            get;
            set;
        }

        public string BuildTaskPath
        {
            get;
            set;
        }

        public ITaskItem[] SourceCodeFiles
        {
            get;
            set;
        }

        public ITaskItem[] TokensFiles
        {
            get;
            set;
        }

        public ITaskItem[] AbstractGrammarFiles
        {
            get;
            set;
        }

        public string GeneratedSourceExtension
        {
            get;
            set;
        }

        public string RootNamespace
        {
            get;
            set;
        }

        public string[] LanguageSourceExtensions
        {
            get;
            set;
        }

        public bool DebugGrammar
        {
            get;
            set;
        }

        public bool ProfileGrammar
        {
            get;
            set;
        }

        [Output]
        public ITaskItem[] GeneratedCodeFiles
        {
            get
            {
                return this._generatedCodeFiles.ToArray();
            }
            set
            {
                this._generatedCodeFiles = new List<ITaskItem>(value);
            }
        }

        public override bool Execute()
        {
            bool success;

            if (!Path.IsPathRooted(AntlrToolPath))
                AntlrToolPath = Path.Combine(Path.GetDirectoryName(BuildEngine.ProjectFileOfTaskNode), AntlrToolPath);

            if (!Path.IsPathRooted(BuildTaskPath))
                BuildTaskPath = Path.Combine(Path.GetDirectoryName(BuildEngine.ProjectFileOfTaskNode), BuildTaskPath);

            try
            {
                AntlrClassGenerationTaskInternal wrapper = CreateBuildTaskWrapper();

                _lock.Wait();
                try
                {
                    success = wrapper.Execute();
                }
                finally
                {
                    _lock.Release();
                }

                if (success)
                {
                    _generatedCodeFiles.AddRange(wrapper.GeneratedCodeFiles.Select(file => (ITaskItem)new TaskItem(file)));
                }

                foreach (BuildMessage message in wrapper.BuildMessages)
                {
                    ProcessBuildMessage(message);
                }
            }
            catch (Exception exception)
            {
                if (IsFatalException(exception))
                    throw;

                ProcessExceptionAsBuildMessage(exception);
                success = false;
            }

            return success;
        }

        private void ProcessExceptionAsBuildMessage(Exception exception)
        {
            ProcessBuildMessage(new BuildMessage(exception.Message));
        }

        private void ProcessBuildMessage(BuildMessage message)
        {
            string logMessage;
            string errorCode;
            errorCode = Log.ExtractMessageCode(message.Message, out logMessage);
            if (string.IsNullOrEmpty(errorCode))
            {
                errorCode = "AC1000";
                logMessage = "Unknown build error: " + message.Message;
            }

            string subcategory = null;
            string helpKeyword = null;

            switch (message.Severity)
            {
            case TraceLevel.Error:
                this.Log.LogError(subcategory, errorCode, helpKeyword, message.FileName, message.LineNumber, message.ColumnNumber, 0, 0, logMessage);
                break;
            case TraceLevel.Warning:
                this.Log.LogWarning(subcategory, errorCode, helpKeyword, message.FileName, message.LineNumber, message.ColumnNumber, 0, 0, logMessage);
                break;
            case TraceLevel.Info:
                this.Log.LogMessage(MessageImportance.Normal, logMessage);
                break;
            case TraceLevel.Verbose:
                this.Log.LogMessage(MessageImportance.Low, logMessage);
                break;
            }
        }

        private AntlrClassGenerationTaskInternal CreateBuildTaskWrapper()
        {
            var wrapper = new AntlrClassGenerationTaskInternal();

            IList<string> sourceCodeFiles = null;
            if (this.SourceCodeFiles != null)
            {
                sourceCodeFiles = new List<string>(SourceCodeFiles.Length);
                foreach (ITaskItem taskItem in SourceCodeFiles)
                    sourceCodeFiles.Add(taskItem.ItemSpec);
            }

            if (this.TokensFiles != null && this.TokensFiles.Length > 0)
            {
                Directory.CreateDirectory(OutputPath);

                HashSet<string> copied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ITaskItem taskItem in TokensFiles)
                {
                    string fileName = taskItem.ItemSpec;
                    if (!File.Exists(fileName))
                    {
                        Log.LogError("The tokens file '{0}' does not exist.", fileName);
                        continue;
                    }

                    string vocabName = Path.GetFileNameWithoutExtension(fileName);
                    if (!copied.Add(vocabName))
                    {
                        Log.LogWarning("The tokens file '{0}' conflicts with another tokens file in the same project.", fileName);
                        continue;
                    }

                    string target = Path.Combine(OutputPath, Path.GetFileName(fileName));
                    if (!Path.GetExtension(target).Equals(".tokens", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.LogError("The destination for the tokens file '{0}' did not have the correct extension '.tokens'.", target);
                        continue;
                    }

                    File.Copy(fileName, target, true);
                    File.SetAttributes(target, File.GetAttributes(target) & ~FileAttributes.ReadOnly);
                }
            }

            wrapper.AntlrToolPath = AntlrToolPath;
            wrapper.SourceCodeFiles = sourceCodeFiles;
            wrapper.TargetLanguage = TargetLanguage;
            wrapper.OutputPath = OutputPath;
            wrapper.RootNamespace = RootNamespace;
            wrapper.GeneratedSourceExtension = GeneratedSourceExtension;
            wrapper.LanguageSourceExtensions = LanguageSourceExtensions;
            wrapper.DebugGrammar = DebugGrammar;
            wrapper.ProfileGrammar = ProfileGrammar;
            return wrapper;
        }

        private static bool IsFatalException(Exception exception)
        {
            while (exception != null)
            {
                if (exception is OutOfMemoryException)
                {
                    return true;
                }

                if (!(exception is TypeInitializationException) && !(exception is TargetInvocationException))
                {
                    break;
                }

                exception = exception.InnerException;
            }

            return false;
        }
    }
}
