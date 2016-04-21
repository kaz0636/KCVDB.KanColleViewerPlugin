﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using KCVDB.Client;
using KCVDB.KanColleViewerPlugin.Properties;
using KCVDB.KanColleViewerPlugin.ViewModels.Metrics;
using Studiotaiha.Toolkit;

namespace KCVDB.KanColleViewerPlugin.ViewModels
{
	class ToolViewViewModel : ViewModelBase, IDisposable
	{
		int MaxHistoryCount { get; } = 20;
		IKCVDBClient Client { get; }
		CompositeDisposable Subscriptions { get; } = new CompositeDisposable();

		public ToolViewViewModel(
			IKCVDBClient client,
			string sessionId,
			IDispatcher dispatcher)
			: base(dispatcher)
		{
			if (client == null) { throw new ArgumentNullException(nameof(client)); }
			if (sessionId == null) { throw new ArgumentNullException(nameof(sessionId)); }
			Client = client;
			SessionId = sessionId;

			// Register metrics
			CustomMetrics.Add(new StartedTimeMetrics(DateTimeOffset.Now));
			CustomMetrics.Add(new TransferredApiCountMetrics(Client));
			CustomMetrics.Add(new SuccessCountMetrics(Client));
			CustomMetrics.Add(new FailureCountMetrics(Client));
			CustomMetrics.Add(new TransferredDataAmountMetrics(Client));
			CustomMetrics.Add(new TransferredDataAmountPerHourMetrics(Client));


			// Register a listener to update history
			Observable.FromEventPattern<ApiDataSentEventArgs>(Client, nameof(Client.ApiDataSent))
				.Select(x => x.EventArgs)
				.Select(x => {
					var now = DateTimeOffset.Now;
					return x.ApiData.Select(apiData => new HistoryItem {
						Time = now,
						Body = apiData.RequestUri.PathAndQuery,
						Success = true,
					});
				})
				.SubscribeOnDispatcher(System.Windows.Threading.DispatcherPriority.Normal)
				.Subscribe(historyItems => {
					HistoryItems = new ObservableCollection<HistoryItem>(
						historyItems.Reverse().Concat(HistoryItems).Take(MaxHistoryCount));
				});

			// Register a listener to notify chinese option chaning
			Subscriptions.Add(Observable.FromEventPattern<PropertyChangedEventArgs>(Settings.Default, nameof(Settings.PropertyChanged))
				.Where(x => x.EventArgs.PropertyName == nameof(Settings.ShowTraditionalChinese))
				.SubscribeOnDispatcher()
				.Subscribe(_ => {
					RaisePropertyChanged(nameof(ShowTraditionalChinese));
				}));

			// Register a listener to receive language switching event
			Subscriptions.Add(Observable.FromEventPattern<PropertyChangedEventArgs>(ResourceHolder.Instance, nameof(ResourceHolder.PropertyChanged))
				.Where(x => x.EventArgs.PropertyName == nameof(ResourceHolder.Culture))
				.SubscribeOnDispatcher()
				.Subscribe(_ => {
					CurrentLanguageTwoLetterName = ResourceHolder.Instance.Culture?.TwoLetterISOLanguageName;
				}));

			CurrentLanguageTwoLetterName = ResourceHolder.Instance.Culture?.TwoLetterISOLanguageName;
		}


		#region Bindings

		#region HistoryItems
		ObservableCollection<HistoryItem> historyItems_ = new ObservableCollection<HistoryItem>();
		public ObservableCollection<HistoryItem> HistoryItems
		{
			get
			{
				return historyItems_;
			}
			set
			{
				SetValue(ref historyItems_, value);
			}
		}
		#endregion

		#region CustomMetrics
		ObservableCollection<IMetrics> metrics_ = new ObservableCollection<IMetrics>();
		public ObservableCollection<IMetrics> CustomMetrics
		{
			get
			{
				return metrics_;
			}
			set
			{
				SetValue(ref metrics_, value);
			}
		}
		#endregion

		#region SessionId
		public string SessionId
		{
			get
			{
				return GetValue<string>();
			}
			set
			{
				SetValue(value);
			}
		}
		#endregion

		#region ShowTraditionalChinese
		public bool ShowTraditionalChinese
		{
			get
			{
				return Settings.Default.ShowTraditionalChinese;
			}
			set
			{
				Settings.Default.ShowTraditionalChinese = value;
				Settings.Default.Save();
			}
		}
		#endregion

		#region CurrentLanguageTwoLetterName
		public string CurrentLanguageTwoLetterName
		{
			get
			{
				return GetValue<string>();
			}
			private set
			{
				if (SetValue(value) || value == null) {
					IsSwitchChineseButtonEnabled = value == "zh";
				}
			}
		}
		#endregion

		#region IsSwitchChineseButtonEnabled
		public bool IsSwitchChineseButtonEnabled
		{
			get
			{
				return GetValue<bool>();
			}
			private set
			{
				SetValue(value);
			}
		}
		#endregion

		#endregion // Bindings


		#region Commands


		#region SwitchChineseCommand
		DelegateCommand switchChineseCommand_ = null;
		public DelegateCommand SwitchChineseCommand
		{
			get
			{
				return switchChineseCommand_ ?? (switchChineseCommand_ = new DelegateCommand(this, nameof(IsSwitchChineseButtonEnabled)) {
					ExecuteHandler = param => {
						ShowTraditionalChinese = !ShowTraditionalChinese;
					},
					CanExecuteHandler = param => {
						return IsSwitchChineseButtonEnabled;
					}
				});
			}
		}
		#endregion

		#endregion // Commands


		#region IDisposable メンバ
		bool isDisposed_ = false;
		virtual protected void Dispose(bool disposing)
		{
			if (isDisposed_) { return; }
			if (disposing) {
				Subscriptions.Dispose();
			}
			isDisposed_ = true;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		#endregion
	}
}
