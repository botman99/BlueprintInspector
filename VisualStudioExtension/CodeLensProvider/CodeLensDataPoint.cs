using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using SharedProject;


#pragma warning disable VSTHRD110

namespace CodeLensProvider
{
	public class CodeLensDataPoint : IAsyncCodeLensDataPoint
	{
		private readonly CodeLensDescriptor descriptor;
		public CodeLensDescriptor Descriptor => this.descriptor;
		public event AsyncEventHandler InvalidatedAsync;

		private string FullyQualifiedName;
		private string class_name;
		private string function_name;

		public List<string> blueprint_asset_list = null;

		public CodeLensDataPoint(CodeLensDescriptor descriptor, string in_FullyQualifiedName, string in_class_name, string in_function_name)
		{
			this.descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

			try
			{
				FullyQualifiedName = in_FullyQualifiedName;
				class_name = in_class_name;
				function_name = in_function_name;

				InitializeBlueprintAssetList(class_name, function_name);
			}
			catch(Exception)
			{
			}
		}

		~CodeLensDataPoint()
		{
			if (CodeLensDataPointGlobals.CodeLensDataPoints.Contains(this))
			{
				CodeLensDataPointGlobals.CodeLensDataPoints.Remove(this);
			}
		}

		public Task<CodeLensDataPointDescriptor> GetDataAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
		{
			try
			{
				if (blueprint_asset_list == null)
				{
					InitializeBlueprintAssetList(class_name, function_name);
				}

				return Task.FromResult(new CodeLensDataPointDescriptor {
					Description = String.Format("{0} Blueprint asset{1}", (blueprint_asset_list == null) ? 0 : blueprint_asset_list.Count, (blueprint_asset_list == null) ? "s" : (blueprint_asset_list.Count == 1) ? "" : "s"),
					IntValue = null,
					TooltipText = "Blueprint assets"
				});
			}
			catch(Exception)
			{
			}

			return Task.FromResult(new CodeLensDataPointDescriptor {
				Description = String.Format(""),
				IntValue = null,
				TooltipText = "Error"
			});
		}

		public Task<CodeLensDetailsDescriptor> GetDetailsAsync(CodeLensDescriptorContext descriptorContext, CancellationToken token)
		{
			try
			{
				if (blueprint_asset_list.Count > 0)
				{
					List<CodeLensDetailHeaderDescriptor> headers = new List<CodeLensDetailHeaderDescriptor>();
					headers.Add(new CodeLensDetailHeaderDescriptor() { DisplayName = "Blueprint Assets", Width = 1.0 });
			
					List<CodeLensDetailEntryDescriptor> entries = new List<CodeLensDetailEntryDescriptor>();

					for (int index = 0; index < blueprint_asset_list.Count; ++index)
					{
						int trim_pos = blueprint_asset_list[index].LastIndexOf(" (");  // trim off the "(x)" substring at the end
						string asset_path = String.Format("{0},{1},{2}", blueprint_asset_list[index].Substring(0, trim_pos), class_name, function_name);

						entries.Add(new CodeLensDetailEntryDescriptor()
							{
								Fields = new List<CodeLensDetailEntryField>()
								{
									new CodeLensDetailEntryField() { Text = blueprint_asset_list[index] },
								},
								NavigationCommand = new CodeLensDetailEntryCommand()
								{
									CommandSet = new Guid("faaf1a9b-f925-4bfb-b76c-7d6d9e9968d1"),
									CommandId = 0x0102,
									CommandName = "OpenAssetPath",
								},
								Tooltip = "Blueprint AssetPath",
								NavigationCommandArgs = new List<string>() { asset_path }
							});
					}

					string copy_args = String.Format("Blueprint assets for {0}", FullyQualifiedName);
					for (int index = 0; index < blueprint_asset_list.Count; ++index)
					{
						copy_args += String.Format(",{0}", blueprint_asset_list[index]);
					}

					var result = new CodeLensDetailsDescriptor()
					{
						Headers = headers,
						Entries = entries,
						PaneNavigationCommands = new List<CodeLensDetailPaneCommand>()
						{
							new CodeLensDetailPaneCommand()
							{
								CommandId = new CodeLensDetailEntryCommand()
								{
									CommandSet = new Guid("faaf1a9b-f925-4bfb-b76c-7d6d9e9968d1"),
									CommandId = 0x0101,
									CommandName = "CopyToClipboard",
								},
								CommandDisplayName = "Copy all to clipboard",
								CommandArgs = new List<string>() { copy_args }
							}
						},
					};

					return Task.FromResult(result);
				}
			}
			catch(Exception)
			{
			}

			return Task.FromResult<CodeLensDetailsDescriptor>(null);
		}

		/// <summary>
		/// Raises <see cref="IAsyncCodeLensDataPoint.Invalidated"/> event.
		/// </summary>
		/// <remarks>
		///  This is not part of the IAsyncCodeLensDataPoint interface.
		///  The data point source can call this method to notify the client proxy that data for this data point has changed.
		/// </remarks>
		public void Invalidate()
		{
			this.InvalidatedAsync?.InvokeAsync(this, EventArgs.Empty).ConfigureAwait(false);
		}

		private void InitializeBlueprintAssetList(string class_name, string function_name)
		{
			if (CodeLensDataPointGlobals.BlueprintJsonData != null)
			{
				int class_index = CodeLensDataPointGlobals.BlueprintJsonData.GetClassIndex(class_name);

				if (class_index != -1)
				{
					int function_index = CodeLensDataPointGlobals.BlueprintJsonData.GetFunctionIndex(class_index, function_name);

					if (function_index != -1)
					{
						blueprint_asset_list = CodeLensDataPointGlobals.BlueprintJsonData.GetBlueprintAssetPaths(class_index, function_index);
					}
				}
			}
		}
	}
}
