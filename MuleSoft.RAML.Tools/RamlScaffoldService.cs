﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using MuleSoft.RAML.Tools.Properties;
using Raml.Common;
using Raml.Tools;
using Raml.Tools.WebApiGenerator;

namespace MuleSoft.RAML.Tools
{
	public class RamlScaffoldService
	{
		private const string RamlSpecVersion = "0.8";
		private const string ControllerBaseTemplateName = "ApiControllerBase.t4";
		private const string ControllerInterfaceTemplateName = "ApiControllerInterface.t4";
		private const string ControllerImplementationTemplateName = "ApiControllerImplementation.t4";
		private const string ModelTemplateName = "ApiModel.t4";
        private const string EnumTemplateName = "ApiEnum.t4";

		private readonly string ContractsFolderName = Settings.Default.ContractsFolderName;
		private readonly IT4Service t4Service;
		private readonly IServiceProvider serviceProvider;
		private readonly TemplatesManager templatesManager = new TemplatesManager();

		public RamlScaffoldService(IT4Service t4Service, IServiceProvider serviceProvider)
		{
			this.t4Service = t4Service;
			this.serviceProvider = serviceProvider;
		}

		public void AddContract(RamlChooserActionParams parameters)
		{
			var dte = serviceProvider.GetService(typeof(SDTE)) as DTE;
			var proj = VisualStudioAutomationHelper.GetActiveProject(dte);
			var folderItem = VisualStudioAutomationHelper.AddFolderIfNotExists(proj, ContractsFolderName);
			var generatedFolderPath = Path.GetDirectoryName(proj.FullName) + Path.DirectorySeparatorChar + ContractsFolderName + Path.DirectorySeparatorChar;

			if (string.IsNullOrWhiteSpace(parameters.RamlSource) && !string.IsNullOrWhiteSpace(parameters.RamlTitle))
				AddEmptyContract(parameters.TargetFileName, parameters.RamlTitle, folderItem, generatedFolderPath, parameters.TargetNamespace, parameters.TargetFileName);
			else
				AddContractFromFile(parameters.RamlFilePath, parameters.TargetNamespace, parameters.RamlSource,
					parameters.DoNotScaffold, folderItem, generatedFolderPath, parameters.TargetFileName);
		}


		public void Scaffold(string ramlSource, string targetNamespace, string ramlFileName)
		{
			var data = RamlScaffolderHelper.GetRamlData(ramlSource, targetNamespace);
			if (data == null || data.Model == null)
				return;

			var model = data.Model;

			var dte = serviceProvider.GetService(typeof(SDTE)) as DTE;
			var proj = VisualStudioAutomationHelper.GetActiveProject(dte);

			var folderItem = VisualStudioAutomationHelper.AddFolderIfNotExists(proj, ContractsFolderName);
			var ramlItem = folderItem.ProjectItems.Cast<ProjectItem>().First(i => i.Name == ramlFileName);
			var generatedFolderPath = Path.GetDirectoryName(proj.FullName) + Path.DirectorySeparatorChar + ContractsFolderName + Path.DirectorySeparatorChar;

            if (!templatesManager.ConfirmWhenIncompatibleServerTemplate(generatedFolderPath,
                new[] { ControllerBaseTemplateName, ControllerInterfaceTemplateName, ControllerImplementationTemplateName, ModelTemplateName, EnumTemplateName }))
                return;

			var extensionPath = Path.GetDirectoryName(GetType().Assembly.Location) + Path.DirectorySeparatorChar;

			
			AddOrUpdateModels(targetNamespace, generatedFolderPath, ramlItem, model, folderItem, extensionPath);

            AddOrUpdateEnums(targetNamespace, generatedFolderPath, ramlItem, model, folderItem, extensionPath);

		    AddOrUpdateControllerBase(targetNamespace, generatedFolderPath, ramlItem, model, folderItem, extensionPath);

		    AddOrUpdateControllerInterfaces(targetNamespace, generatedFolderPath, ramlItem, model, folderItem, extensionPath);

		    AddOrUpdateControllerImplementations(targetNamespace, generatedFolderPath, proj, model, folderItem, extensionPath);
		}

	    private void AddOrUpdateControllerImplementations(string targetNamespace, string generatedFolderPath, Project proj,
	        WebApiGeneratorModel model, ProjectItem folderItem, string extensionPath)
	    {
	        templatesManager.CopyServerTemplateToProjectFolder(generatedFolderPath, ControllerImplementationTemplateName,
	            Settings.Default.ControllerImplementationTemplateTitle);
	        var controllersFolderItem = VisualStudioAutomationHelper.AddFolderIfNotExists(proj, "Controllers");
	        var controllersFolderPath = Path.GetDirectoryName(proj.FullName) + Path.DirectorySeparatorChar + "Controllers" +
	                                    Path.DirectorySeparatorChar;
	        var templatesFolder = Path.Combine(generatedFolderPath, "Templates");
	        var controllerImplementationTemplateParams =
	            new TemplateParams<ControllerObject>(Path.Combine(templatesFolder, ControllerImplementationTemplateName),
	                controllersFolderItem, "controllerObject", model.Controllers, controllersFolderPath, folderItem,
	                extensionPath, targetNamespace, "Controller", false);
	        controllerImplementationTemplateParams.Title = Settings.Default.ControllerImplementationTemplateTitle;
	        GenerateCodeFromTemplate(controllerImplementationTemplateParams);
	    }

	    private void AddOrUpdateControllerInterfaces(string targetNamespace, string generatedFolderPath, ProjectItem ramlItem,
	        WebApiGeneratorModel model, ProjectItem folderItem, string extensionPath)
	    {
	        templatesManager.CopyServerTemplateToProjectFolder(generatedFolderPath, ControllerInterfaceTemplateName,
	            Settings.Default.ControllerInterfaceTemplateTitle);
            var templatesFolder = Path.Combine(generatedFolderPath, "Templates");
	        var controllerInterfaceParams =
	            new TemplateParams<ControllerObject>(Path.Combine(templatesFolder, ControllerInterfaceTemplateName),
	                ramlItem, "controllerObject", model.Controllers, generatedFolderPath, folderItem, extensionPath,
	                targetNamespace, "Controller", true, "I");
	        controllerInterfaceParams.Title = Settings.Default.ControllerInterfaceTemplateTitle;
	        GenerateCodeFromTemplate(controllerInterfaceParams);
	    }

	    private void AddOrUpdateControllerBase(string targetNamespace, string generatedFolderPath, ProjectItem ramlItem,
	        WebApiGeneratorModel model, ProjectItem folderItem, string extensionPath)
	    {
	        templatesManager.CopyServerTemplateToProjectFolder(generatedFolderPath, ControllerBaseTemplateName,
	            Settings.Default.BaseControllerTemplateTitle);
            var templatesFolder = Path.Combine(generatedFolderPath, "Templates");
	        var controllerBaseTemplateParams =
	            new TemplateParams<ControllerObject>(Path.Combine(templatesFolder, ControllerBaseTemplateName),
	                ramlItem, "controllerObject", model.Controllers, generatedFolderPath, folderItem, extensionPath,
	                targetNamespace, "Controller");
	        controllerBaseTemplateParams.Title = Settings.Default.BaseControllerTemplateTitle;
	        GenerateCodeFromTemplate(controllerBaseTemplateParams);
	    }

	    private void AddOrUpdateModels(string targetNamespace, string generatedFolderPath, ProjectItem ramlItem,
	        WebApiGeneratorModel model, ProjectItem folderItem, string extensionPath)
	    {
	        templatesManager.CopyServerTemplateToProjectFolder(generatedFolderPath, ModelTemplateName,
	            Settings.Default.ModelsTemplateTitle);
            var templatesFolder = Path.Combine(generatedFolderPath, "Templates");
	        var apiObjectTemplateParams = new TemplateParams<ApiObject>(
	            Path.Combine(templatesFolder, ModelTemplateName), ramlItem, "apiObject", model.Objects.Values,
	            generatedFolderPath, folderItem, extensionPath, targetNamespace);
	        apiObjectTemplateParams.Title = Settings.Default.ModelsTemplateTitle;
	        GenerateCodeFromTemplate(apiObjectTemplateParams);
	    }

        private void AddOrUpdateEnums(string targetNamespace, string generatedFolderPath, ProjectItem ramlItem,
            WebApiGeneratorModel model, ProjectItem folderItem, string extensionPath)
        {
            templatesManager.CopyServerTemplateToProjectFolder(generatedFolderPath, EnumTemplateName,
                Settings.Default.EnumsTemplateTitle);
            var templatesFolder = Path.Combine(generatedFolderPath, "Templates");
            var apiEnumTemplateParams = new TemplateParams<ApiEnum>(
                Path.Combine(templatesFolder, EnumTemplateName), ramlItem, "apiEnum", model.Enums,
                generatedFolderPath, folderItem, extensionPath, targetNamespace);
            apiEnumTemplateParams.Title = Settings.Default.ModelsTemplateTitle;
            GenerateCodeFromTemplate(apiEnumTemplateParams);
        }


	    public void UpdateRaml(string ramlFilePath)
		{
			var refFilePath = InstallerServices.GetRefFilePath(ramlFilePath);
			var ramlSource = RamlReferenceReader.GetRamlSource(refFilePath);
			var contents = GetRamlContentsFromSource(ramlSource);

			File.WriteAllText(ramlFilePath, contents);
		}

		private static string GetRamlContentsFromSource(string ramlSource)
		{
			if (ramlSource.StartsWith("http"))
				return RamlContentsFromWebSource(ramlSource);
			
			return File.ReadAllText(ramlSource);
		}

		private static string RamlContentsFromWebSource(string ramlSource)
		{
			Uri uri;
			if (Uri.TryCreate(ramlSource, UriKind.Absolute, out uri))
				return Downloader.GetContents(uri);

			var errorMessage = "Invalid Url specified: " + uri.AbsoluteUri;
			ActivityLog.LogError(VisualStudioAutomationHelper.RamlVsToolsActivityLogSource, errorMessage);
			throw new UriFormatException(errorMessage);
		}

		private void AddContractFromFile(string ramlFilePath, string targetNamespace, string ramlSource, bool? doNotScaffold, ProjectItem folderItem, string folderPath, string targetFilename)
		{
			InstallerServices.AddRefFile(ramlFilePath, targetNamespace, ramlSource, folderPath, targetFilename);

			var includesFolderPath = folderPath + Path.DirectorySeparatorChar + InstallerServices.IncludesFolderName;

			var includesManager = new RamlIncludesManager();
			var result = includesManager.Manage(ramlSource, includesFolderPath, confirmOverrite: true);

			var includesFolderItem = folderItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == InstallerServices.IncludesFolderName);
			if (includesFolderItem == null)
				includesFolderItem = folderItem.ProjectItems.AddFolder(InstallerServices.IncludesFolderName);

			foreach (var file in result.IncludedFiles)
			{
				includesFolderItem.ProjectItems.AddFromFile(file);
			}

			//var existingIncludeItems = includesFolderItem.ProjectItems.Cast<ProjectItem>();
			//var oldIncludedFiles = existingIncludeItems.Where(item => !result.IncludedFiles.Contains(item.FileNames[0]));
			//InstallerServices.RemoveSubItemsAndAssociatedFiles(oldIncludedFiles);

			var ramlProjItem = AddOrUpdateRamlFile(result.ModifiedContents, folderItem, folderPath, Path.GetFileName(ramlFilePath));
			InstallerServices.RemoveSubItemsAndAssociatedFiles(ramlProjItem);

			if (doNotScaffold == null || !doNotScaffold.Value)
				Scaffold(ramlProjItem.FileNames[0], targetNamespace, Path.GetFileName(ramlFilePath));
		}

		private static ProjectItem AddOrUpdateRamlFile(string modifiedContents, ProjectItem folderItem, string folderPath, string ramlFileName)
		{
			ProjectItem ramlProjItem;
			var ramlDestFile = Path.Combine(folderPath, ramlFileName);

			if (File.Exists(ramlDestFile))
			{
				var dialogResult = InstallerServices.ShowConfirmationDialog(ramlFileName);

				if (dialogResult == MessageBoxResult.Yes)
				{
					File.WriteAllText(ramlDestFile, modifiedContents);
					ramlProjItem = folderItem.ProjectItems.AddFromFile(ramlDestFile);
				}
				else
				{
					ramlProjItem = folderItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == ramlFileName);
					if (ramlProjItem == null)
						ramlProjItem = folderItem.ProjectItems.AddFromFile(ramlDestFile);
				}
			}
			else
			{
				File.WriteAllText(ramlDestFile, modifiedContents);
				ramlProjItem = folderItem.ProjectItems.AddFromFile(ramlDestFile);
			}
			return ramlProjItem;
		}

		private void AddEmptyContract(string filename, string title, ProjectItem folderItem, string folderPath, string targetNamespace, string targetFilename)
		{
			
			var newContractFile = Path.Combine(folderPath, filename);
			var contents = CreateNewRamlContents(title);

			InstallerServices.AddRefFile(newContractFile, targetNamespace, newContractFile, folderPath, targetFilename);

			if (File.Exists(newContractFile))
			{
				var dialogResult = InstallerServices.ShowConfirmationDialog(filename);
				if (dialogResult == MessageBoxResult.Yes)
				{
					File.WriteAllText(newContractFile, contents);
					folderItem.ProjectItems.AddFromFile(newContractFile);
				}
				else
				{
					var item = folderItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == newContractFile);
					if (item == null)
						folderItem.ProjectItems.AddFromFile(newContractFile);
				}
			}
			else
			{
				File.WriteAllText(newContractFile, contents);
				folderItem.ProjectItems.AddFromFile(newContractFile);
			}
		}

		private static string CreateNewRamlContents(string title)
		{
			var contents = "#%RAML " + RamlSpecVersion + Environment.NewLine +
			               "title: " + title + Environment.NewLine;
			return contents;
		}

	    public class TemplateParams<TT> where TT : IHasName
	    {
	        private string _templatePath;
	        private ProjectItem _projItem;
	        private string _parameterName;
	        private IEnumerable<TT> _parameterCollection;
	        private string _folderPath;
	        private ProjectItem _folderItem;
	        private string _binPath;
	        private string _targetNamespace;
	        private string _suffix;
	        private bool _ovewrite;
	        private string _prefix;

	        public TemplateParams(string templatePath, ProjectItem projItem, string parameterName, IEnumerable<TT> parameterCollection, string folderPath, ProjectItem folderItem, string binPath, string targetNamespace, string suffix = null, bool ovewrite = true, string prefix = null)
	        {
	            _templatePath = templatePath;
	            _projItem = projItem;
	            _parameterName = parameterName;
	            _parameterCollection = parameterCollection;
	            _folderPath = folderPath;
	            _folderItem = folderItem;
	            _binPath = binPath;
	            _targetNamespace = targetNamespace;
	            _suffix = suffix;
	            _ovewrite = ovewrite;
	            _prefix = prefix;
	        }

	        public string TemplatePath
	        {
	            get { return _templatePath; }
	        }

	        public ProjectItem ProjItem
	        {
	            get { return _projItem; }
	        }

	        public string ParameterName
	        {
	            get { return _parameterName; }
	        }

	        public IEnumerable<TT> ParameterCollection
	        {
	            get { return _parameterCollection; }
	        }

	        public string FolderPath
	        {
	            get { return _folderPath; }
	        }

	        public ProjectItem FolderItem
	        {
	            get { return _folderItem; }
	        }

	        public string BinPath
	        {
	            get { return _binPath; }
	        }

	        public string TargetNamespace
	        {
	            get { return _targetNamespace; }
	        }

	        public string Suffix
	        {
	            get { return _suffix; }
	        }

	        public bool Ovewrite
	        {
	            get { return _ovewrite; }
	        }

	        public string Prefix
	        {
	            get { return _prefix; }
	        }

	        public string Title { get; set; }
	    }

	    private void GenerateCodeFromTemplate<T>(TemplateParams<T> templateParams) where T : IHasName
		{

			foreach (var parameter in templateParams.ParameterCollection)
			{
				var generatedFileName = GetGeneratedFileName(templateParams.Suffix, templateParams.Prefix, parameter);

				var result = t4Service.TransformText(templateParams.TemplatePath, templateParams.ParameterName, parameter, templateParams.BinPath, templateParams.TargetNamespace);
				var destinationFile = Path.Combine(templateParams.FolderPath, generatedFileName);
				var contents = templatesManager.AddServerMetadataHeader(result.Content, Path.GetFileNameWithoutExtension(templateParams.TemplatePath), templateParams.Title);
				
				if(templateParams.Ovewrite || !File.Exists(destinationFile))
				{
					File.WriteAllText(destinationFile, contents);
				}

				// add file if it does not exist
				var fileItem = templateParams.ProjItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == generatedFileName);
				if (fileItem != null) continue;

				if (templateParams.ProjItem.Name.EndsWith(".raml"))
				{
					var alreadyIncludedInProj = IsAlreadyIncludedInProject(templateParams.FolderPath, templateParams.FolderItem, generatedFileName, templateParams.ProjItem);
					if (!alreadyIncludedInProj)
						templateParams.ProjItem.ProjectItems.AddFromFile(destinationFile);
				}
				else
				{
					templateParams.ProjItem.ProjectItems.AddFromFile(destinationFile);
				}
			}
		}

		private static bool IsAlreadyIncludedInProject(string folderPath, ProjectItem folderItem, string generatedFileName, ProjectItem fileItem)
		{
			var otherRamlFiles = GetOtherRamlFilesInProject(folderPath, fileItem);
			var alreadyIncludedInProj = false;
			foreach (var ramlFile in otherRamlFiles)
			{
				var fileName = Path.GetFileName(ramlFile);
				var otherRamlFileItem =
					folderItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == fileName);

				if (otherRamlFileItem == null) continue;
				var item = otherRamlFileItem.ProjectItems.Cast<ProjectItem>().FirstOrDefault(i => i.Name == generatedFileName);
				alreadyIncludedInProj = alreadyIncludedInProj || (item != null);
			}
			return alreadyIncludedInProj;
		}

		private static IEnumerable<string> GetOtherRamlFilesInProject(string folderPath, ProjectItem fileItem)
		{
			var ramlFiles = Directory.EnumerateFiles(folderPath, "*.raml").ToArray();
			var currentRamlFile = fileItem.FileNames[0];
			var otherRamlFiles =
				ramlFiles.Where(f => !String.Equals(f, currentRamlFile, StringComparison.InvariantCultureIgnoreCase));
			return otherRamlFiles;
		}

		private static string GetGeneratedFileName<T>(string suffix, string prefix, T parameter) where T : IHasName
		{
			var name = parameter.Name;
			if (!string.IsNullOrWhiteSpace(prefix))
				name = prefix + name;
			if (!string.IsNullOrWhiteSpace(suffix))
				name += suffix;

			var generatedFileName = name + ".cs";
			return generatedFileName;
		}


	}
}