﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

using NuGet.Protocol.Plugins;

namespace NuGetPe.AssemblyMetadata
{
    public class AssemblyDebugData
    {
        private readonly static Version minVersionWithReproducible = new Version(3, 9, 0);
        public AssemblyDebugData()
        {
            SourceLinkErrors = new List<string>();
            Sources = new List<AssemblyDebugSourceDocument>();
            SymbolKeys = new List<SymbolKey>();
            MetadataReferences = new List<MetadataReference>();
            CompilerFlags = new List<CompilerFlag>();

            _untrackedSources = new Lazy<IReadOnlyList<string>>(() => GetNonEmbeddedSourcesInObjDir());
            _sourcesAreDeterministic = new Lazy<bool>(CalculateSourcesDeterministic);
        }

        private readonly Lazy<IReadOnlyList<string>> _untrackedSources;
        private readonly Lazy<bool> _sourcesAreDeterministic;

        public PdbType PdbType { get; internal set; }

        public IReadOnlyList<AssemblyDebugSourceDocument> Sources { get; internal set; }
        public IReadOnlyList<string> SourceLinkErrors { get; internal set; }
        public IReadOnlyList<SymbolKey> SymbolKeys { get; internal set; }

        public IReadOnlyCollection<MetadataReference> MetadataReferences { get; internal set;}
        public IReadOnlyCollection<CompilerFlag> CompilerFlags { get; internal set;}

        public bool PdbChecksumIsValid { get; internal set; }

        public bool HasSourceLink => Sources.Any(doc => doc.HasSourceLink);

        public bool AllSourceLink => Sources.All(doc => doc.HasSourceLink);

        /// <summary>
        /// True if we hae PDB data loaded
        /// </summary>
        public bool HasDebugInfo { get; internal set; }

        public bool HasCompilerFlags => CompilerFlags.Count > 0 && MetadataReferences.Count > 0;

        public IReadOnlyList<string> UntrackedSources => _untrackedSources.Value;

        public bool SourcesAreDeterministic => _sourcesAreDeterministic.Value;

        private IReadOnlyList<string> GetNonEmbeddedSourcesInObjDir()
        {
            // get sources where /obj/ is in the name and it's not
            // Document names may use either / or \ a directory separator

            var docs = (from doc in Sources
                        let path = doc.Name.Replace('\\', '/')
                        where (path.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
                               path.Contains("/temp/", StringComparison.OrdinalIgnoreCase) ||
                               path.Contains("/tmp/", StringComparison.OrdinalIgnoreCase) ) && !doc.IsEmbedded
                        select doc.Name).ToList();

            return docs;
        }

        public bool CompilerVersionSupportsReproducible
        {
            get
            {
                if (!HasCompilerFlags)
                    return false;


                // Get the version


                // If it's 3.9.0- or lower -> false
                // 3.9.0 (without the -) -> true
                // 3.9.1 or higher (with the - is ok) -> true

                var versionString = CompilerFlags.Where(f => f.Key == "compiler-version")
                                           .Select(f => f.Value)
                                           .FirstOrDefault();

                // We should not get this as the compiler should always write this, but check anyway
                if (versionString == null)
                    return false;

                // In the format of something like 3.7.0-6.20418.4+9b878f99b53dafab14e253210b5570e2a68d0010
                if(versionString.Contains('-', StringComparison.OrdinalIgnoreCase))
                {
                    if(Version.TryParse(versionString.Split('-')[0], out var version))
                    {
                        // as this has a -, make sure we're > 3.9.0
                        return version > minVersionWithReproducible;
                    }
                }
                else
                {
                    // see if it has build data and split, then only keep the numeric part
                    if(versionString.Contains('+', StringComparison.OrdinalIgnoreCase))
                    {
                        versionString = versionString.Split('+')[0];
                    }

                    if (Version.TryParse(versionString.Split('-')[0], out var version))
                    {
                        // as this does not have a -, make sure we're >= 3.9.0
                        return version >= minVersionWithReproducible;
                    }
                }

                // Malformed data
                return false;
            }
        }

        private bool CalculateSourcesDeterministic()
        {
            return Sources.All(doc => doc.Name.StartsWith("/_", StringComparison.OrdinalIgnoreCase));
        }

    }

    public enum PdbType
    {
        Portable,
        Embedded,
        Full
    }
}
