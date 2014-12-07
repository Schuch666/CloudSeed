﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

namespace CloudSeed.UI
{
	public class CloudSeedViewModel : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly object updateLock = new object();
		private readonly CloudSeedPlugin plugin;
		private bool suppressUpdates;
		private Parameter? activeControl;
		private ProgramBanks.PluginProgram selectedProgram;
		private string newProgramName;

		public CloudSeedViewModel(CloudSeedPlugin plugin)
		{
			this.plugin = plugin;
			NumberedParameters = new ObservableCollection<double>();
			foreach (var para in Enum.GetValues(typeof(Parameter)).Cast<Parameter>())
				NumberedParameters.Add(0.0);

			SaveProgramCommand = new DelegateCommand(x => SaveProgram());
			RenameProgramCommand = new DelegateCommand(x => RenameProgram());
			LoadProgramCommand = new DelegateCommand(LoadProgram);

			NumberedParameters.CollectionChanged += (s, e) =>
			{
				lock (updateLock)
				{
					if (suppressUpdates)
						return;

					var para = (Parameter)e.NewStartingIndex;
					var val = (double)e.NewItems[0];
					plugin.SetParameter(para, val, false, true);
					NotifyChanged(() => ActiveControlDisplay);
				}
			};

			LoadProgram(ProgramBanks.Bank.UserPrograms.FirstOrDefault() ?? new ProgramBanks.PluginProgram { Name = "Default Program" });
		}

		public ICommand SaveProgramCommand { get; private set; }
		public ICommand RenameProgramCommand { get; private set; }
		public ICommand LoadProgramCommand { get; private set; }

		public ObservableCollection<double> NumberedParameters { get; private set; }

		public Parameter? ActiveControl
		{
			get { return activeControl; }
			set
			{
				activeControl = value; 
				NotifyChanged(() => ActiveControlName);
				NotifyChanged(() => ActiveControlDisplay);
			}
		}

		public string ActiveControlName
		{
			get { return ActiveControl.HasValue ? ActiveControl.Value.NameWithSpaces() : ""; }
		}

		public string ActiveControlDisplay
		{
			get { return ActiveControl.HasValue ? plugin.GetDisplay(ActiveControl.Value) : ""; }
		}

		public ProgramBanks.PluginProgram[] FactoryPrograms
		{
			get { return ProgramBanks.Bank.FactoryPrograms.ToArray(); }
		}

		public ProgramBanks.PluginProgram[] UserPrograms
		{
			get { return ProgramBanks.Bank.UserPrograms.ToArray(); }
		}

		public ProgramBanks.PluginProgram SelectedProgram
		{
			get { return selectedProgram; }
			set { selectedProgram = value; NotifyChanged(); }
		}

		public string NewProgramName
		{
			get { return newProgramName; }
			set { newProgramName = value; NotifyChanged(); }
		}

		public void UpdateParameter(Parameter param, double newValue)
		{
			lock (updateLock)
			{
				suppressUpdates = true;
				NumberedParameters[param.Value()] = newValue;

				suppressUpdates = false;
				NotifyChanged(() => NumberedParameters);
			}
		}

		private void LoadProgram(object obj)
		{
			var programData = obj as ProgramBanks.PluginProgram;
			if (programData == null)
				return;

			if (programData.Data != null)
				plugin.SetJsonProgram(programData.Data);

			SelectedProgram = programData;
		}

		private void SaveProgram()
		{
			var jsonData = plugin.GetJsonProgram();
			ProgramBanks.Bank.SaveProgram(NewProgramName, jsonData, true);
			NotifyChanged(() => UserPrograms);
		}

		private void RenameProgram()
		{
			NotifyChanged(() => UserPrograms);
		}

		#region Notify Change

		// I used this and GetPropertyName to avoid having to hard-code property names
		// into the NotifyChange events. This makes the application much easier to refactor
		// leter on, if needed.
		private void NotifyChanged<T>(System.Linq.Expressions.Expression<Func<T>> exp)
		{
			var name = GetPropertyName(exp);
			NotifyChanged(name);
		}

		private void NotifyChanged([CallerMemberName]string property = null)
		{
			if (PropertyChanged != null)
				PropertyChanged.Invoke(this, new PropertyChangedEventArgs(property));
		}

		private static string GetPropertyName<T>(System.Linq.Expressions.Expression<Func<T>> exp)
		{
			return (((System.Linq.Expressions.MemberExpression)(exp.Body)).Member).Name;
		}

		#endregion
	}
}
