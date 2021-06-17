using HACS.Core;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using static Utilities.Utility;

namespace HACS.Components
{

	public class Actuator : ManagedDevice, IActuator, Actuator.IDevice, Actuator.IConfig
	{
		#region Device interfaces
		public new interface IDevice : ManagedDevice.IDevice
		{
			/// <summary>
			/// The actuator is presently being operated by the controller.
			/// </summary>
			bool Active { get; set; }

			/// <summary>
			/// The current or most recent actuator operation undertaken.
			/// </summary>
			IActuatorOperation Operation { get; set; }

			bool InMotion { get; set; }
			double Elapsed { get; set; }
		}

		public new interface IConfig : ManagedDevice.IConfig { }

		public new IDevice Device => this;
		public new IConfig Config => this;

		#endregion Device interfaces

		/// <summary>
		/// The names of the operations supported by the actuator.
		/// </summary>
		public virtual List<string> Operations
		{ 
			get 
			{
				var names = new List<string>();
				foreach (var op in ActuatorOperations)
					names.Add(op.Name);
				return names;
			}
		}

		[JsonProperty]
		public virtual ObservableItemsCollection<ActuatorOperation> ActuatorOperations
		{ 
			get => actuatorOperations;
			set
			{
				if (actuatorOperations != null)
				{
					(actuatorOperations as INotifyPropertyChanged).PropertyChanged -= OnOperationChanged;
					actuatorOperations.ItemPropertyChanged -= OnOperationChanged;
				}
				if (value != null)
				{
					value.ItemPropertyChanged -= OnOperationChanged;
					value.ItemPropertyChanged += OnOperationChanged;
					OnOperationChanged(value, null);
				}
				Ensure(ref actuatorOperations, value);
			}
		}
		ObservableItemsCollection<ActuatorOperation> actuatorOperations;

		//[JsonProperty]
		//public virtual ObservableItemsList<ActuatorOperation> ActuatorOperations
		//{
		//	get => actuatorOperations;
		//	set => Ensure(ref actuatorOperations, value, OnOperationChanged);
		//}
		//ObservableItemsList<ActuatorOperation> actuatorOperations;
		protected virtual void OnOperationChanged(object sender, PropertyChangedEventArgs e) { }

		public virtual bool Ready => Manager?.Ready ?? false;

		/// <summary>
		/// The communications link has been established and
		/// data has been received.
		/// </summary>
		public virtual bool Linked => Ready && Active && UpdatesReceived > 0;

		/// <summary>
		/// Its controller is currently operating this device.
		/// </summary>
		public virtual bool Active
		{
			get => active;
			protected set => Set(ref active, value);
		}
		bool active = false;
		bool IDevice.Active
		{
			get => Active;
			set
			{
				lock (PendingOperationsLocker)
				{
					if (active && !value)
						PendingOperations--;
					else if (!active && value)
						StopRequested = false;
					Active = value;
					if (value)
						Device.UpdatesReceived = 0;
				}
			}
		}

		protected object PendingOperationsLocker = new object();
		public int PendingOperations
		{ 
			get => pendingOperations;
			protected set => Ensure(ref pendingOperations, value); 
		}
		int pendingOperations = 0;

		public virtual bool Idle => PendingOperations == 0;

		/// <summary>
		/// Set by controller when the actuator becomes Active, this value
		/// persists (as indication of the prior operation) after the actuator
		/// becomes inactive. Before setting this property to a new value, 
		/// the new value should be validated using
		/// Actuator.ValidateOperation(value).
		/// </summary>		
		public virtual IActuatorOperation Operation
		{
			get => operation;
			protected set => Set(ref operation, value);
		}
		IActuatorOperation operation;
		IActuatorOperation IDevice.Operation
		{
			get => Operation;
			set => Operation = value;
		}

		public virtual double Elapsed
		{
			get => elapsed;
			protected set => Ensure(ref elapsed, value);
		}
		double elapsed;
		double IDevice.Elapsed
		{
			get => Elapsed;
			set => Elapsed = value;
		}

		public virtual double TimeLimit { get; set; }

		/// <summary>
		/// The controller detected that the operation time limit was reached.
		/// This value is false if TimeLimit is 0 (no limit set).
		/// Note that this value is also false if the controller hasn't checked.
		/// </summary>
		public virtual bool TimeLimitDetected
		{
			get => UpdatesReceived > 0 &&
				TimeLimit > 0 &&
				Elapsed >= TimeLimit;
			protected set { }
		}

		public virtual bool InMotion
		{
			get => inMotion;
			protected set => Ensure(ref inMotion, value);
		}
		bool inMotion = false;

		bool IDevice.InMotion
		{
			get => InMotion;
			set => InMotion = value;
		}

		/// <summary>
		/// Motion is prevented by a condition detected by the controller:
		/// a limit switch was engaged, a CurrentLimit was detected, or
		/// the operation reached its TimeLimit.
		/// </summary>
		public virtual bool MotionInhibited { get; protected set; } = false;

		/// <summary>
		/// A Stop() request was received after the current or prior operation started.
		/// This value is reset by ActuatorController during initial operation 
		/// configuration.
		/// </summary>
		public virtual bool StopRequested
		{
			get => stopRequested;
			protected set => Ensure(ref stopRequested, value);
		}
		bool stopRequested = false;

		public new virtual bool Stopped { get => !InMotion; protected set { } }

		public virtual bool ActionSucceeded { get => Operation == null || MotionInhibited; protected set { } }

		public IActuatorOperation FindOperation(string operationName)
		{
			foreach (var op in ActuatorOperations)
				if (op.Name == operationName)
					return op;
			return null;
		}

		public virtual IActuatorOperation ValidateOperation(IActuatorOperation operation)
		{
			return operation;  // everything is valid by default
		}

		/// <summary>
		/// Executes the requested actuator operation. If the 
		/// operationName is empty, null, or not supported, a 
		/// "Select" functionality is performed.
		/// </summary>
		/// <param name="operationName"></param>
		public virtual void DoOperation(string operationName)
		{
			if (operationName == "Stop")
				Stop();
			else
				DoOperation(FindOperation(operationName));
		}

		public virtual void DoOperation(IActuatorOperation operation)
		{
			lock (PendingOperationsLocker) { PendingOperations++; }
			NotifyConfigChanged(operation.Name);
		}

		public virtual void Stop() =>
			StopRequested = true;

		/// <summary>
		/// Wait until all scheduled operations for the actuator are finished.
		/// </summary>
		public virtual void WaitForIdle() => WaitForCondition(() => Idle, -1, 35);

		public Actuator(IHacsDevice d = null) : base(d) { }

	}
}
