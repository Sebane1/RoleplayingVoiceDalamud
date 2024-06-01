using Dalamud.Plugin;
using Penumbra.Api.IpcSubscribers;
using Glamourer.Api.IpcSubscribers;

public class PenumbraAndGlamourerIpcWrapper {
    public static PenumbraAndGlamourerIpcWrapper Instance { get; private set; }

    private GetModDirectory _getModDirectory;

    public GetCollection GetCollection { get => _getCollection; set => _getCollection = value; }
    public SetCollection SetCollection { get => _setCollection; set => _setCollection = value; }
    public GetCollectionForObject GetCollectionForObject { get => _getCollectionForObject; set => _getCollectionForObject = value; }
    public SetCollectionForObject SetCollectionForObject { get => _setCollectionForObject; set => _setCollectionForObject = value; }
    public GetModList GetModList { get => _getModList; set => _getModList = value; }
    public AddMod AddMod { get => _addMod; set => _addMod = value; }
    public ReloadMod ReloadMod { get => _reloadMod; set => _reloadMod = value; }
    public GetCurrentModSettings GetCurrentModSettings { get => _getCurrentModSettings; set => _getCurrentModSettings = value; }
    public GetAvailableModSettings GetAvailableModSettings { get => _getAvailableModSettings; set => _getAvailableModSettings = value; }
    public TrySetMod TrySetMod { get => _trySetMod; set => _trySetMod = value; }
    public TrySetModPriority TrySetModPriority { get => _trySetModPriority; set => _trySetModPriority = value; }
    public TrySetModPriority TrySetModPriority1 { get => _trySetModPriority; set => _trySetModPriority = value; }
    public TrySetModSetting TrySetModSetting { get => _trySetModSetting; set => _trySetModSetting = value; }
    public TrySetModSettings TrySetModSettings { get => _trySetModSettings; set => _trySetModSettings = value; }
    public GetDesignList GetDesignList { get => _getDesignList; set => _getDesignList = value; }
    public ApplyDesign ApplyDesign { get => _applyDesign; set => _applyDesign = value; }
    public SetItem SetItem { get => _setItem; set => _setItem = value; }
    public GetStateBase64 GetStateBase64 { get => _getStateBase64; set => _getStateBase64 = value; }
    public GetChangedItemsForCollection GetChangedItemsForCollection { get => _getChangedItemsForCollection; set => _getChangedItemsForCollection = value; }
    public GetModDirectory GetModDirectory { get => _getModDirectory; set => _getModDirectory = value; }
    public RedrawObject RedrawObject { get => _redrawObject; set => _redrawObject = value; }

    private GetCollection _getCollection;
    private SetCollection _setCollection;
    private GetCollectionForObject _getCollectionForObject;
    private SetCollectionForObject _setCollectionForObject;
    private GetChangedItemsForCollection _getChangedItemsForCollection;
    private GetModList _getModList;
    private AddMod _addMod;
    private ReloadMod _reloadMod;
    private GetCurrentModSettings _getCurrentModSettings;
    private GetAvailableModSettings _getAvailableModSettings;
    private TrySetMod _trySetMod;
    private TrySetModPriority _trySetModPriority;
    private TrySetModSetting _trySetModSetting;
    private TrySetModSettings _trySetModSettings;
    private RedrawObject _redrawObject;
    private GetDesignList _getDesignList;
    private ApplyDesign _applyDesign;
    private SetItem _setItem;
    private GetStateBase64 _getStateBase64;

    public PenumbraAndGlamourerIpcWrapper(DalamudPluginInterface dalamudPluginInterface) {
        Instance = this;
        _getModDirectory = new GetModDirectory(dalamudPluginInterface);
        _getCollection = new GetCollection(dalamudPluginInterface);
        _setCollection = new SetCollection(dalamudPluginInterface);
        _getCollectionForObject = new GetCollectionForObject(dalamudPluginInterface);
        _setCollectionForObject = new SetCollectionForObject(dalamudPluginInterface);
        _getChangedItemsForCollection = new GetChangedItemsForCollection(dalamudPluginInterface);
        _getModList = new GetModList(dalamudPluginInterface);
        _addMod = new AddMod(dalamudPluginInterface);
        _reloadMod = new ReloadMod(dalamudPluginInterface);
        _getCurrentModSettings = new GetCurrentModSettings(dalamudPluginInterface);
        _getAvailableModSettings = new GetAvailableModSettings(dalamudPluginInterface);
        _trySetMod = new TrySetMod(dalamudPluginInterface);
        _trySetModPriority = new TrySetModPriority(dalamudPluginInterface);
        _trySetModSetting = new TrySetModSetting(dalamudPluginInterface);
        _trySetModSettings = new TrySetModSettings(dalamudPluginInterface);
        _redrawObject = new RedrawObject(dalamudPluginInterface);

        _getDesignList = new GetDesignList(dalamudPluginInterface);
        _applyDesign = new ApplyDesign(dalamudPluginInterface);
        _setItem = new SetItem(dalamudPluginInterface);
        _getStateBase64 = new GetStateBase64(dalamudPluginInterface);
    }
}
