using Newtonsoft.Json;

namespace HACS.Components
{
	public class FlowChamber : Chamber, IFlowChamber
	{
        #region HacsComponent
        protected override void Connect()
		{
			base.Connect();
			FlowManager = Find<FlowManager>(flowManagerName);
		}
		#endregion HacsComponent

		[JsonProperty("FlowManager")]
		string FlowManagerName { get => FlowManager?.Name; set => flowManagerName = value; }
		string flowManagerName;
		public IFlowManager FlowManager
		{ 
			get => flowManager;
			set => Ensure(ref flowManager, value);
		}
		IFlowManager flowManager;

		public IRxValve FlowValve => FlowManager?.FlowValve;

		//[JsonProperty("BypassValve")]
		//string bypassValveName { get => BypassValve?.Name; set => _bypassValveName = value; }
		//string _bypassValveName;
		//public IValve BypassValve { get; set; }

	}
}
