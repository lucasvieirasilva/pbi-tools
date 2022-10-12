
// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Linq;
using PowerArgs;

namespace PbiTools.Cli
{
    using System.Threading;
    using Model;
    using PowerBI;
    using ProjectSystem;
    using Utils;

    public partial class CmdLineActions
    {

#if NETFRAMEWORK
        [ArgActionMethod, ArgShortcut("extract-model")]
        [ArgDescription("Extracts the model of a PBIX/PBIT file into a folder structure. By default, this will create a sub-folder in the directory of the *.pbix file with the same name without the extension.")]
        [ArgExample(
            @"pbi-tools extract-model '.\data\Samples\Adventure Works DW 2020.pbix' -extractFolder '.\data\Samples\Adventure Works DW 2020 - Raw' -modelSerialization Raw", 
            "Extracts the PBIX file into the specified extraction folder (relative to the current working dir), using the 'Auto' compatibility mode. The model part is serialialized using Raw mode.",
            Title = "Extract: Custom folder and settings")]
        [ArgExample(
            @"pbi-tools extract-model '.\data\Samples\Adventure Works DW 2020.pbix'", 
            "Extracts the specified PBIX file into the default extraction folder (relative to the PBIX file location), using the 'Auto' compatibility mode. Any settings specified in the '.pbixproj.json' file already present in the destination folder will be honored.",
            Title = "Extract: Default")]
        public void ExtractModel(
            [ArgRequired(IfNot = "pid"), ArgExistingFile, ArgDescription("The path to an existing PBIX file.")]
                string pbixPath,
            [ArgRequired(IfNot = "pbixPath"), ArgDescription("The Power BI Desktop process ID to extract sources from (look up via 'pbi-tools info').")]
                int? pid,
            [ArgDescription("The port number from a running Power BI Desktop instance (look up via 'pbi-tools info'). When specified, the model will not be read from the PBIX file, and will instead be retrieved from the PBI instance. Only supported for V3 PBIX files.")]
            [ArgCantBeCombinedWith("pid"), ArgRange(1024, 65535)]
                int? pbiPort,
            [ArgDescription("The folder to extract the PBIX file to. Only needed to override the default location. Can be relative to current working directory.")]
                string extractFolder,
            [ArgDescription("The extraction mode."), ArgDefaultValue(ExtractActionCompatibilityMode.Auto)]
                ExtractActionCompatibilityMode mode,
            [ArgDescription("The model serialization mode.")]
                ModelSerializationMode modelSerialization,
            [ArgDescription("The mashup serialization mode.")]
                MashupSerializationMode mashupSerialization
        )
        {
            // TODO Support '-parts' parameter, listing specifc parts to extract only
            // ReportSerializationMode: Default, Raw, Enhanced

            PowerBIProcess pbiProc = default;

            if (pid.HasValue) {
                pbiProc = PowerBIProcesses.EnumerateProcesses().FirstOrDefault(p => p.ProcessId == pid);
                if (pbiProc == null)
                    throw new PbiToolsCliException(ExitCode.InvalidArgs, $"No running Power BI Desktop instance with PID = {pid} found.");
                if (pbiProc.PbixPath == null)
                    throw new PbiToolsCliException(ExitCode.PathNotFound, $"Could not detect PBIX file path for Power BI Desktop instance with PID = {pid}.");

                Log.Information("Extracting model from file at {PbixPath} and local AS instance at Port: {Port}.", pbiProc.PbixPath, pbiProc.Port);

                pbixPath = pbiProc.PbixPath;
                pbiPort = pbiProc.Port;
            }

            var targetFolder = String.IsNullOrEmpty(extractFolder) 
                ? null 
                : new DirectoryInfo(extractFolder).FullName;

            using (var reader = new PbixReader(pbixPath, _dependenciesResolver))
            {
                if (mode == ExtractActionCompatibilityMode.Legacy)
                {
                    // TODO Check -pbiPort not specified
                    using (var extractor = new Actions.PbixExtractAction(reader))
                    {
                        extractor.ExtractModel();
                    }
                }
                else
                {
                    try
                    {
                        using (var model = PbixModel.FromReader(reader, targetFolder, pbiPort))
                        {
                            if (modelSerialization != default)
                                model.PbixProj.Settings.Model.SerializationMode = modelSerialization;

                            if (mashupSerialization != default)
                                model.PbixProj.Settings.Mashup.SerializationMode = mashupSerialization;

                            model.ModelToFolder(path: targetFolder);
                        }
                    }
                    catch (NotSupportedException) when (mode == ExtractActionCompatibilityMode.Auto)
                    {
                        using (var extractor = new Actions.PbixExtractAction(reader))
                        {
                            extractor.ExtractModel();
                        }
                    }
                }
            }

            Console.WriteLine($"Completed in {_stopWatch.Elapsed}.");
        }  
#endif
    }
}