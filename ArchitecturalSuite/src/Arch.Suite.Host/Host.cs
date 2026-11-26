using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AICad.Contracts;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using App = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Arch.Suite.Host
{
    public class Host : ICadManagedAddin, IExtensionApplication
    {
        private static PaletteSet palette;
        private static SuitePanel panel;
        private static Document observing;
        private static string pending;
        private static bool changed;
        private static bool eventsRegistered;
        private static bool protecting;
        private static string failureHandle;
        private static Document failureDocument;
        public IReadOnlyList<CadRibbonPin> ManagedCommands => new[]
        {
            new CadRibbonPin { Command = "QAARCHOPEN", Label = "Architectural Suite" }
        };
        public void Initialize() { EnsureEvents(); }
        private static void EnsureEvents()
        {
            if(eventsRegistered)return;eventsRegistered=true;
            App.DocumentManager.DocumentActivated+=Activated;App.DocumentManager.DocumentToBeDestroyed+=Closing;
            Observe(App.DocumentManager.MdiActiveDocument);
        }
        public void InitializeManaged(Action<string> callback) { EnsureEvents(); }
        public void Terminate()
        {
            App.DocumentManager.DocumentActivated -= Activated;
            App.DocumentManager.DocumentToBeDestroyed -= Closing;
            eventsRegistered=false;Observe(null);
            palette?.Dispose(); palette = null; panel = null; pending = null;
        }
        internal static void ApplyTheme(System.Windows.Window window)
        {
            if (panel == null) return;
            window.Resources = panel.Resources;
            window.Background = (System.Windows.Media.Brush)panel.Resources["ToolBackground"];
            window.Foreground = (System.Windows.Media.Brush)panel.Resources["ToolText"];
            window.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            window.FontSize = 12;
        }
        internal static void ShowResult(string message) { if (panel != null) panel.Status.Text = message; }
        internal static void ShowCandidates(List<Candidates.Candidate> rows)
        {
            if (panel != null) panel.LegacyCandidates.ItemsSource = rows;
            ShowResult("Luồng 0.2: " + rows.Count + " ứng viên, xem tab Dữ liệu 0.2.");
        }
        private static void Observe(Document doc)
        {
            if (doc == observing) return;
            if (observing != null)
            {
                observing.Database.ObjectModified -= Modified;
                observing.Database.ObjectErased -= Erased;
                observing.CommandEnded -= Ended;
                observing.ImpliedSelectionChanged -= Selected;
                observing.CommandCancelled -= Ended;
                observing.CommandFailed -= Ended;
            }
            observing = doc; changed = false;
            if (observing != null)
            {
                observing.Database.ObjectModified += Modified;
                observing.Database.ObjectErased += Erased;
                observing.CommandEnded += Ended;
                observing.ImpliedSelectionChanged += Selected;
                observing.CommandCancelled += Ended;
                observing.CommandFailed += Ended;
            }
        }
        private static void Selected(object sender, EventArgs args)
        {
            if (pending != null || panel == null || observing == null) return;
            var selected = observing.Editor.SelectImplied();
            if (selected.Status != PromptStatus.OK || selected.Value.Count != 1) return;
            string handle = selected.Value.GetObjectIds()[0].Handle.ToString();
            var part = panel.LegacyParts.Items.OfType<Part>().FirstOrDefault(p => p.Handles.Contains(handle));
            if (part != null) { panel.LegacyParts.SelectedItem = part; panel.LegacyParts.ScrollIntoView(part); }
            var item = panel.Candidates.Items.OfType<FrameCandidate>().FirstOrDefault(c => c.Handle == handle);
            if (item != null) { panel.Candidates.SelectedItem = item; panel.Candidates.ScrollIntoView(item); }
        }
        private static void Modified(object sender, ObjectEventArgs args)
        {
            if (pending == null && (args.DBObject is Entity||args.DBObject is LayerTableRecord||args.DBObject is BlockTableRecord)) changed = true;
        }
        private static void Erased(object sender, ObjectErasedEventArgs args)
        {
            if (pending == null) changed = true;
        }
        private static void Ended(object sender, CommandEventArgs args)
        {
            if (pending != null || protecting || observing == null) return;
            bool undo = new[] { "U", "UNDO", "REDO", "MREDO" }.Contains(args.GlobalCommandName, StringComparer.OrdinalIgnoreCase);
            if (!changed && !undo) return;
            changed = false;
            try
            {
                protecting=true;
                try
                {
                    using(observing.LockDocument())using(var tx=observing.Database.TransactionManager.StartTransaction())
                    {
                        var data=FrameDocument.Load(observing.Database,tx);
                        if(DrawingLinks.Protect(observing.Database,tx,data)){data.Save(observing.Database,tx);observing.Editor.WriteMessage("\nArch: đã phục hồi nội dung liên kết theo bản đã duyệt. Sửa thiết kế tại nguồn.");}
                        tx.Commit();
                    }
                }
                finally{protecting=false;}
                Refresh(observing);
                ShowResult("DWG đã thay đổi. Quét lại khung để xem phạm vi và đối tượng hiện hành.");
            }
            catch (System.Exception error) { ShowResult(error.Message); }
        }
        private static void Activated(object sender, DocumentCollectionEventArgs args)
        {
            failureHandle=null;failureDocument=null;Observe(args.Document);
            panel?.ClearSelection();
            if (panel != null) panel.LegacyCandidates.ItemsSource = null;
            if (args.Document == null) return;
            try { Refresh(args.Document); ShowResult("Đã đọc dữ liệu từ DWG. Chọn khung để tiếp tục."); }
            catch (System.Exception error) { ShowResult(error.Message); }
        }
        private static void Closing(object sender, DocumentCollectionEventArgs args)
        {
            if (observing != args.Document) return;
            Observe(null);
            if (panel == null) return;
            panel.SetData("", new List<FrameSnapshot>(), new Model());
            ShowResult("Mở DWG để tiếp tục.");
        }
        internal static void Refresh(Document doc)
        {
            if (panel == null) return;
            Observe(doc);
            using (var tx = doc.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var frames = FrameDocument.Load(doc.Database, tx);
                DrawingLinks.CheckCopies(doc.Database,tx,frames);
                var snapshots = ReadFrames(doc.Database, tx, frames);
                panel.ClearBatch();
                panel.SetData(doc.Name, snapshots, Model.Load(tx, doc.Database));
                panel.SetDefinitions(frames);
                foreach(var item in frames.Registrations)if(Model.Resolve(tx,doc.Database,item.Handle)==null)item.Status="Nguồn đã xóa; chờ đồng bộ / gỡ đăng ký";
                panel.SetRegistered(frames,snapshots);
            }
        }
        private static List<FrameSnapshot> ReadFrames(Database db, Transaction tx, FrameDocument data)
        {
            return data.Frames.Select(frame =>
            {
                try { return FrameReader.Read(db, tx, frame, false); }
                catch (InvalidOperationException error) { return new FrameSnapshot { Frame = frame, Title = frame.BlockName, Status = error.Message }; }
            }).ToList();
        }
        private static async void Request(string key)
        {
            if (key == "column-up" || key == "column-down") { panel?.MoveColumn(key == "column-up" ? -1 : 1); return; }
            if(key=="frame-batch-level")
            {
                try{panel?.ApplyBatchLevel();}catch(InvalidOperationException error){ShowResult(error.Message);}return;
            }
            if (pending != null) return;
            var doc = App.DocumentManager.MdiActiveDocument;
            if (doc == null) { ShowResult("Mở DWG để tiếp tục."); return; }
            if (Convert.ToInt32(App.GetSystemVariable("CMDACTIVE")) != 0) { ShowResult("Kết thúc lệnh CAD hiện tại rồi thử lại."); return; }
            if(key=="frame-link-preview"||key=="frame-link-create"||key=="frame-link-sync"){failureHandle=null;failureDocument=null;}
            pending = key; if (panel != null) panel.IsEnabled = false;
            try
            {
                await App.DocumentManager.ExecuteInCommandContextAsync(_ =>
                {
                    try
                    {
                        if (doc != App.DocumentManager.MdiActiveDocument)
                            throw new InvalidOperationException("DWG đã đổi. Chọn lại thao tác.");
                        using (doc.LockDocument())
                        {
                            if (key == "library-status") { var state=ResourceLayers.CurrentStatus();panel.SetLibraryStatus(state);ShowResult(state); }
                            else if (key.StartsWith("frame-", StringComparison.Ordinal)) FrameAction(doc, key);
                            else
                            {
                                if (key == "scan") new Inventory().Scan();
                                else Workspace.Run(doc, key);
                                Refresh(doc);
                            }
                        }
                    }
                    catch (OperationCanceledException) { ShowResult("Đã hủy thao tác."); }
                    catch (System.Exception error)
                    {
                        if(error is SourceFailure sourceError){failureHandle=sourceError.SourceHandle;failureDocument=doc;}
                        var message=error is Rainmaker.Library.Reader.LibraryReadException libraryError?libraryError.Code+" · "+libraryError.Message:error.Message;
                        var log=SuiteDiagnostics.Write(doc,key,error);if(log!=null)message+="\nChi tiết: "+log;
                        ShowResult(message);doc.Editor.WriteMessage("\n"+message);
                    }
                    return Task.CompletedTask;
                }, null);
            }
            catch (System.Exception error) { ShowResult(error.Message); }
            finally { pending = null; changed = false; if (panel != null) panel.IsEnabled = true; }
        }
        private static void FrameAction(Document doc, string key)
        {
            if (key == "frame-refresh") { Refresh(doc); ShowResult("Đã đọc lại cấu hình đã lưu trong DWG."); return; }
            using (var tx = doc.Database.TransactionManager.StartTransaction())
            {
                var data = FrameDocument.Load(doc.Database, tx);
                if(key=="frame-registration-remove")
                {
                    var selected=panel.RegisteredSources.SelectedItem as SourceRegistration;
                    if(selected==null)throw new InvalidOperationException("Chọn dòng cấu kiện đã đăng ký.");
                    if(data.Registrations.RemoveAll(r=>r.Id==selected.Id)!=1)throw new InvalidOperationException("Dữ liệu đã đổi. Đọc lại DWG.");
                    data.Save(doc.Database,tx);tx.Commit();Refresh(doc);
                    ShowResult("Đã gỡ đăng ký; hình nguồn được giữ nguyên. Save DWG để giữ thay đổi.");return;
                }
                if(key=="frame-config-export")
                {
                    var sections=DefinitionTransfer.ChooseExport();if(sections==null)throw new OperationCanceledException();
                    var bundle=DefinitionTransfer.Export(data,Model.Load(tx,doc.Database),sections);
                    var output=new Microsoft.Win32.SaveFileDialog{Title="Xuất bộ định nghĩa Arch",Filter="Arch JSON (*.json)|*.json",DefaultExt=".json",FileName="Arch_DinhNghia.json",OverwritePrompt=true};
                    if(output.ShowDialog()!=true)throw new OperationCanceledException();DefinitionTransfer.Write(output.FileName,bundle);ShowResult("Đã xuất bộ định nghĩa: "+output.FileName);return;
                }
                if(key=="frame-config-import")
                {
                    var input=new Microsoft.Win32.OpenFileDialog{Title="Nạp bộ định nghĩa Arch",Filter="Arch JSON (*.json)|*.json",CheckFileExists=true};
                    if(input.ShowDialog()!=true)throw new OperationCanceledException();
                    var bundle=DefinitionTransfer.Read(input.FileName);var rows=DefinitionTransfer.Preview(doc.Database,tx,data,bundle);
                    if(!DefinitionTransfer.ChooseImport(rows))throw new OperationCanceledException();
                    var proposed=DefinitionTransfer.Apply(data,rows);proposed.Save(doc.Database,tx);tx.Commit();Refresh(doc);
                    ShowResult("Đã nạp "+rows.Count(r=>r.Use)+" mục. Không sửa hình nguồn; kiểm tra/đăng ký lại khi cần. Save DWG để giữ cấu hình.");return;
                }
                if(key=="frame-group-layer")
                {
                    var selected=doc.Editor.GetEntity("\nChọn đối tượng lấy layer chèn: ");if(selected.Status!=PromptStatus.OK)throw new OperationCanceledException();
                    var entity=(Entity)tx.GetObject(selected.ObjectId,OpenMode.ForRead);
                    if(entity.OwnerId!=SymbolUtilityServices.GetBlockModelSpaceId(doc.Database))throw new InvalidOperationException("Chọn đối tượng trực tiếp trong Model.");
                    var existing=panel.GroupLayers.Text.Split(';').Select(v=>v.Trim()).Where(v=>v!="").ToList();existing.Add(entity.Layer);
                    panel.GroupLayers.Text=string.Join("; ",existing.Distinct(StringComparer.OrdinalIgnoreCase));return;
                }
                if(key.StartsWith("frame-floor-",StringComparison.Ordinal))
                {
                    var floors=panel.ReadFloors();
                    if(key=="frame-floor-add"){floors.Add(new FloorDefinition{Name="Tầng mới "+(floors.Count+1)});panel.Floors.Items.Refresh();ShowResult("Nhập cao tầng/dầm rồi Lưu tầng.");return;}
                    if(key=="frame-floor-remove")
                    {
                        var selected=panel.Floors.SelectedItem as FloorDefinition;if(selected==null)throw new InvalidOperationException("Chọn tầng cần bỏ.");
                        if(data.Frames.Any(f=>f.FloorId==selected.Id))throw new InvalidOperationException("Tầng đang được khung sử dụng.");floors.Remove(selected);panel.Floors.Items.Refresh();return;
                    }
                    FloorCatalog.Apply(data,floors);
                    data.Save(doc.Database,tx);tx.Commit();Refresh(doc);ShowResult("Đã lưu và đồng bộ tầng với khung. Tầng chưa có cao tầng/dầm được giữ ở trạng thái chờ; các phần chịu ảnh hưởng cần cập nhật.");return;
                }
                if(key=="frame-wall-beam")
                {
                    var selected=panel.RegisteredSources.SelectedItem as SourceRegistration;
                    if(selected==null||selected.Kind!="Tường")throw new InvalidOperationException("Chọn một tường đã đăng ký.");
                    var original=data.Registrations.Single(r=>r.Id==selected.Id);var frame=data.Frames.Single(f=>f.Id==original.FrameId);
                    var snapshot=FrameReader.Read(doc.Database,tx,frame,true);var candidate=snapshot.Candidates.SingleOrDefault(c=>c.Handle==original.Handle);
                    if(candidate==null)throw new InvalidOperationException("Tường đã ra ngoài khung/mất nguồn.");
                    double? beam=string.IsNullOrWhiteSpace(panel.BeamOverride.Text)?(double?)null:SuitePanel.Nonnegative(panel.BeamOverride,"Cao dầm riêng");
                    RegistrationService.Upsert(data,SuiteKinds.Register(doc.Database,tx,data,snapshot,candidate,beam));data.Save(doc.Database,tx);tx.Commit();Refresh(doc);ShowResult("Đã cập nhật dầm riêng / kế thừa tầng cho tường.");return;
                }
                if(key.StartsWith("frame-link-",StringComparison.Ordinal))
                {
                    var selected=data.Frames.SingleOrDefault(f=>f.Id==panel.SelectedId);
                    var drawing=(panel.LinkedDrawings.SelectedItem as LinkedDrawing);
                    drawing=drawing==null?data.Drawings.SingleOrDefault(d=>d.TargetFrameId==selected?.Id):data.Drawings.SingleOrDefault(d=>d.Id==drawing.Id);
                    var source=key=="frame-link-create"?DrawingLinks.Source(data,selected):drawing==null?DrawingLinks.Source(data,selected):data.Frames.Single(f=>f.Id==drawing.SourceFrameId);
                    if(key=="frame-link-preview")
                    {
                        var preview=DrawingLinks.Preview(doc.Database,tx,data,source,panel.LinkKinds,drawing);ShowResult(preview);doc.Editor.WriteMessage("\n"+preview);return;
                    }
                    if(key=="frame-link-create"||key=="frame-link-sync")
                    {
                        if(panel.LinkKinds.Count==0)throw new InvalidOperationException("Chọn nhóm cần liên kết.");
                        var preview=DrawingLinks.Preview(doc.Database,tx,data,source,panel.LinkKinds,key=="frame-link-create"?null:drawing);
                        doc.Editor.WriteMessage("\n"+preview+"\nNhóm bỏ chọn sẽ ngắt và giữ hình độc lập.");
                        var approval=new PromptKeywordOptions("\nDuyệt thay đổi [Apply/Skip] <Skip>: ");approval.Keywords.Add("Apply");approval.Keywords.Add("Skip");approval.Keywords.Default="Skip";
                        var answer=doc.Editor.GetKeywords(approval);if(answer.Status!=PromptStatus.OK||answer.StringResult!="Apply")throw new OperationCanceledException();
                    }
                    if(key=="frame-link-create")
                    {
                        var point=doc.Editor.GetPoint("\nVị trí mốc chèn khung đích: ");if(point.Status!=PromptStatus.OK)throw new OperationCanceledException();
                        DrawingLinks.Create(doc.Database,tx,data,source,panel.DrawingName.Text.Trim(),panel.DrawingCode.Text.Trim(),point.Value.TransformBy(doc.Editor.CurrentUserCoordinateSystem),panel.LinkKinds);
                    }
                    else
                    {
                        if(drawing==null)throw new InvalidOperationException("Chọn dòng bản vẽ liên kết cần thao tác.");
                        if(key=="frame-link-sync")DrawingLinks.Sync(doc.Database,tx,data,drawing,panel.LinkKinds);
                        else if(key=="frame-link-unlink")DrawingLinks.Unlink(doc.Database,tx,drawing);
                        else if(key=="frame-link-move")
                        {var point=doc.Editor.GetPoint("\nVị trí mới của khung đích: ");if(point.Status!=PromptStatus.OK)throw new OperationCanceledException();DrawingLinks.Move(doc.Database,tx,data,drawing,point.Value.TransformBy(doc.Editor.CurrentUserCoordinateSystem));}
                    }
                    data.Save(doc.Database,tx);tx.Commit();Refresh(doc);ShowResult("Đã cập nhật bản vẽ liên kết. Save DWG để giữ quan hệ và hình.");return;
                }
                if(key=="frame-door-sample")
                {
                    var options=new PromptEntityOptions("\nChọn block cửa dynamic mẫu: ");
                    options.SetRejectMessage("\nChọn block cửa.");options.AddAllowedClass(typeof(BlockReference),false);
                    var selected=doc.Editor.GetEntity(options);
                    if(selected.Status!=PromptStatus.OK)throw new OperationCanceledException();
                    var sample=RegistrationService.Describe(tx,(BlockReference)tx.GetObject(selected.ObjectId,OpenMode.ForRead));
                    panel.ShowDoorSample(sample);
                    ShowResult("Đọc block "+sample.BlockName+". Chọn property rộng và nguồn chiều cao ở tab Định nghĩa tường / cửa.");
                    return;
                }
                if(key=="frame-definitions-save")
                {
                    var wall=panel.ReadWallRule();var door=panel.ReadDoorRule();
                    var rules=data.DoorRules.Where(d=>door==null||!string.Equals(d.BlockName,door.BlockName,StringComparison.OrdinalIgnoreCase)||!string.Equals(d.TypeCode??"",door.TypeCode??"",StringComparison.OrdinalIgnoreCase)).ToList();
                    if(panel.DefinitionKind!="Tường"&&panel.DefinitionKind!="Cửa đi"&&panel.DefinitionKind!="Cửa sổ")
                    {
                        var layers=panel.GroupLayers.Text.Split(new[]{';',',','\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(v=>v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        var table=(LayerTable)tx.GetObject(doc.Database.LayerTableId,OpenMode.ForRead);
                        if(layers.Count==0||layers.Any(v=>!table.Has(v)))throw new InvalidOperationException("Chọn ít nhất một layer có trong DWG.");
                        data.LayerGroups.RemoveAll(g=>g.Kind==panel.DefinitionKind);data.LayerGroups.Add(new LayerGroupDefinition{Kind=panel.DefinitionKind,Layers=layers});
                    }
                    if((panel.DefinitionKind=="Cửa đi"||panel.DefinitionKind=="Cửa sổ")&&door==null)throw new InvalidOperationException("Chọn block mẫu và khai báo cửa trước khi lưu.");
                    if(door!=null)rules.Add(door);
                    RegistrationService.Validate(wall,rules);
                    data.WallRule=wall;data.DoorRules=rules;data.Save(doc.Database,tx);tx.Commit();
                    panel.SetDefinitions(data);
                    ShowResult("Đã lưu định nghĩa tường và "+rules.Count+" loại cửa trong DWG. Save DWG để giữ khi chuyển máy.");
                    return;
                }
                if(key=="frame-profile")
                {
                    var frame=panel.ReadDraft();
                    var check=FrameReader.Read(doc.Database,tx,frame,false);
                    RegistrationService.ValidateFrame(frame,check);
                    data.Profile=FrameProfile.From(frame);data.Save(doc.Database,tx);tx.Commit();
                    panel.SetDefinitions(data);ShowResult("Đã lưu profile. Có thể Quét chọn khung cùng mẫu.");
                    return;
                }
                if(key=="frame-batch")
                {
                    if(data.Profile==null)throw new InvalidOperationException("Chọn khung mẫu, setup tag/biên rồi Lưu profile khung trước.");
                    if(!doc.Database.TileMode)throw new InvalidOperationException("Quét chọn khung trong Model.");
                    var options=new PromptSelectionOptions{MessageForAdding="\nQuét chọn các khung cùng mẫu: "};
                    var selected=doc.Editor.GetSelection(options,new SelectionFilter(new[]{new TypedValue(0,"INSERT")}));
                    if(selected.Status!=PromptStatus.OK)throw new OperationCanceledException();
                    var batch=new List<FrameSnapshot>();int ignored=0;
                    foreach(ObjectId id in selected.Value.GetObjectIds())
                    {
                        var block=tx.GetObject(id,OpenMode.ForRead) as BlockReference;
                        if(block==null||block.OwnerId!=SymbolUtilityServices.GetBlockModelSpaceId(doc.Database)){ignored++;continue;}
                        var name=((BlockTableRecord)tx.GetObject(block.IsDynamicBlock?block.DynamicBlockTableRecord:block.BlockTableRecord,OpenMode.ForRead)).Name;
                        if(name!=data.Profile.BlockName){ignored++;continue;}
                        string handle=block.Handle.ToString();
                        var frame=data.Frames.SingleOrDefault(f=>f.Handle==handle)??data.Profile.ForBlock(handle);
                        FrameSnapshot snapshot;
                        try
                        {
                            snapshot=FrameReader.Read(doc.Database,tx,frame,false);
                            RegistrationService.ValidateFrame(frame,snapshot);
                            snapshot.Status="Chờ lưu";
                        }
                        catch(InvalidOperationException error){snapshot=new FrameSnapshot{Frame=frame,Title=name,Status=error.Message};}
                        batch.Add(snapshot);
                    }
                    if(batch.Count==0)throw new InvalidOperationException("Không có khung trùng mẫu "+data.Profile.BlockName+" trong tập chọn.");
                    panel.ShowBatch(doc.Name,ReadFrames(doc.Database,tx,data),batch,Model.Load(tx,doc.Database),data);
                    ShowResult("Tìm "+batch.Count+" khung cùng mẫu; bỏ qua "+ignored+" block khác. Sửa tầng từng dòng rồi Lưu các khung hợp lệ.");
                    return;
                }
                if(key=="frame-save-batch")
                {
                    panel.CommitFrameEdits();int count=0;var errors=new List<string>();var failed=new List<FrameSnapshot>();
                    foreach(var item in panel.BatchFrames.ToList())
                    {
                        try
                        {
                            var check=FrameReader.Read(doc.Database,tx,item.Frame,false);
                            RegistrationService.ValidateFrame(item.Frame,check);
                            data.Frames.RemoveAll(f=>f.Id==item.Frame.Id);
                            SuiteKinds.BindFloor(data,item.Frame);data.Frames.Add(item.Frame);count++;
                        }
                        catch(InvalidOperationException error){item.Status=error.Message;failed.Add(item);errors.Add("#"+item.Frame.Handle+": "+error.Message);}
                    }
                    if(count>0){data.Save(doc.Database,tx);tx.Commit();Refresh(doc);}
                    if(failed.Count>0)
                    {
                        using(var read=doc.Database.TransactionManager.StartOpenCloseTransaction())
                            panel.ShowBatch(doc.Name,ReadFrames(doc.Database,read,data),failed,Model.Load(read,doc.Database),data);
                    }
                    ShowResult("Đã lưu "+count+" khung, "+errors.Count+" lỗi. "+string.Join(" · ",errors.Take(3))+" Save DWG để giữ dữ liệu.");
                    return;
                }
                if(key=="frame-register-walls"||key=="frame-register-doors"||key=="frame-register-all")
                {
                    RegistrationService.Validate(data.WallRule,data.DoorRules);
                    var frame=data.Frames.SingleOrDefault(f=>f.Id==panel.SelectedId);
                    if(frame==null||frame.IsDerived)throw new InvalidOperationException("Chọn khung nguồn đã lưu để đăng ký; không đăng ký hình đích.");
                    if(frame.WallLayer!=data.WallRule.Layer)throw new InvalidOperationException("Lưu khung với layer vùng tường khớp định nghĩa trước.");
                    if(key=="frame-register-doors" && data.DoorRules.Count==0)
                        throw new InvalidOperationException("Chọn block cửa mẫu và Lưu định nghĩa trước khi đăng ký cửa.");
                    var snapshot=FrameReader.Read(doc.Database,tx,frame,true);
                    var candidates=snapshot.Candidates.Where(c=>key=="frame-register-all"||key=="frame-register-walls"&&c.Layer.Equals(data.WallRule.Layer,StringComparison.OrdinalIgnoreCase)||key=="frame-register-doors"&&(c.Kind=="Block dynamic"||c.Kind=="Block")).ToList();
                    int count=0;var errors=new List<string>();
                    foreach(var candidate in candidates)
                    {
                        try
                        {
                            var kind=SuiteKinds.Classify(tx,Model.Resolve(tx,doc.Database,candidate.Handle),data);
                            if(kind==null||key=="frame-register-doors"&&kind!="Cửa đi"&&kind!="Cửa sổ")continue;
                            var previous=data.Registrations.SingleOrDefault(r=>r.Handle==candidate.Handle);
                            var source=SuiteKinds.Register(doc.Database,tx,data,snapshot,candidate,previous?.BeamOverrideMm);
                            RegistrationService.Upsert(data,source);count++;
                        }
                        catch(InvalidOperationException error){errors.Add("#"+candidate.Handle+": "+error.Message);candidate.Reason=error.Message;candidate.Registered="Chưa đăng ký";}
                    }
                    if(count>0){data.Save(doc.Database,tx);tx.Commit();Refresh(doc);}
                    MarkRegistrations(snapshot,data);
                    panel.SelectSnapshot(snapshot);using(var read=doc.Database.TransactionManager.StartOpenCloseTransaction())panel.SetRegistered(data,ReadFrames(doc.Database,read,data));
                    ShowResult("Đã đăng ký/cập nhật "+count+", bỏ qua "+errors.Count+". "+string.Join(" · ",errors.Take(3)));
                    return;
                }
                if (key == "frame-pick")
                {
                    if (!doc.Database.TileMode) throw new InvalidOperationException("Chọn block khung tại tab Model.");
                    var options = new PromptEntityOptions("\nChọn block khung mẫu: ");
                    options.SetRejectMessage("\nCần chọn block khung.");
                    options.AddAllowedClass(typeof(BlockReference), false);
                    var picked = doc.Editor.GetEntity(options);
                    if (picked.Status != PromptStatus.OK) throw new OperationCanceledException();
                    string handle = picked.ObjectId.Handle.ToString();
                    var frame = data.Frames.FirstOrDefault(f => f.Handle == handle);
                    if (frame != null) panel.SelectSnapshot(FrameReader.Read(doc.Database, tx, frame, true));
                    else
                    {
                        var block = FrameReader.Resolve(doc.Database, tx, handle);
                        string name = ((BlockTableRecord)tx.GetObject(block.IsDynamicBlock ?
                            block.DynamicBlockTableRecord : block.BlockTableRecord, OpenMode.ForRead)).Name;
                        frame = data.Profile!=null && data.Profile.BlockName==name?
                            data.Profile.ForBlock(handle):new DrawingFrame { Handle = handle, BlockName = name };
                        var draft = FrameReader.Read(doc.Database, tx, frame, false);
                        frame.NameTag = frame.NameTag ?? draft.Attributes.Keys.FirstOrDefault(t => t.Equals("TENBANVE", StringComparison.OrdinalIgnoreCase))
                            ?? draft.Attributes.Keys.FirstOrDefault();
                        foreach (var tag in draft.Attributes.Keys)
                            if(!frame.Columns.Any(c=>c.Tag==tag)) frame.Columns.Add(new FrameColumn { Tag = tag, Label = tag });
                        var suggested = draft.BoundaryLayers.Where(n => n.Equals("QA_KH_Quet", StringComparison.OrdinalIgnoreCase) ||
                            n.Equals("QA_KH_quét", StringComparison.OrdinalIgnoreCase)).ToList();
                        if (string.IsNullOrWhiteSpace(frame.BoundaryLayer) && suggested.Count == 1) frame.BoundaryLayer = suggested[0];
                        draft.Frame = frame; panel.SelectSnapshot(draft);
                    }
                    ShowResult("Bản nháp khung. Chọn tag, layer biên, nhập tầng rồi Lưu khung / setup.");
                    return;
                }
                if (key == "frame-export")
                {
                    var frames = ReadFrames(doc.Database, tx, data);
                    if (frames.Count == 0) throw new InvalidOperationException("Đăng ký khung trước khi xuất danh mục.");
                    var output = doc.Editor.GetFileNameForSave("Tên CSV danh mục mới");
                    if (output.Status != PromptStatus.OK) throw new OperationCanceledException();
                    var labels = frames.SelectMany(f => f.Frame.Columns).Where(c => c.Use).Select(c => c.Label).Distinct().ToList();
                    Func<string, string> quote = s => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
                    using (var file = new FileStream(Path.ChangeExtension(output.StringResult, ".csv"), FileMode.CreateNew))
                    using (var writer = new StreamWriter(file, new UTF8Encoding(true)))
                    {
                        writer.WriteLine(string.Join(",", new[] { "Tầng" }.Concat(labels).Select(quote)));
                        foreach (var frame in frames)
                            writer.WriteLine(string.Join(",", new[] { frame.Frame.Level }.Concat(labels.Select(label =>
                            {
                                var column = frame.Frame.Columns.FirstOrDefault(c => c.Use && c.Label == label);
                                return column == null ? "" : FrameReader.Attribute(frame, column.Tag);
                            })).Select(quote)));
                    }
                    ShowResult("Đã xuất danh mục từ cấu hình khung đã lưu.");
                    return;
                }
                if(key=="frame-locate"||key=="frame-registration-locate"||key=="frame-error-locate")
                {
                    string handle=key=="frame-error-locate"?(failureDocument==doc?failureHandle:null):key=="frame-registration-locate"?(panel.RegisteredSources.SelectedItem as SourceRegistration)?.Handle:(panel.Candidates.SelectedItem as FrameCandidate)?.Handle;
                    ViewNavigation.Locate(doc,tx,handle);ShowResult("Đã zoom và chọn đối tượng nguồn #"+handle+". Giữ nguyên hướng nhìn.");return;
                }
                var draftFrame = panel.ReadDraft();
                if (key == "frame-select")
                    draftFrame = data.Frames.SingleOrDefault(f => f.Id == panel.SelectedId) ?? draftFrame;
                if (string.IsNullOrWhiteSpace(draftFrame.BoundaryLayer)) throw new InvalidOperationException("Chọn layer của biên trong block.");
                var selectedFrameSnapshot = FrameReader.Read(doc.Database, tx, draftFrame, true);
                MarkRegistrations(selectedFrameSnapshot,data);
                if (key == "frame-save")
                {
                    if(draftFrame.IsDerived)throw new InvalidOperationException("Khung đích quản lý qua Bản vẽ liên kết; sửa thiết kế tại nguồn.");
                    if (string.IsNullOrWhiteSpace(draftFrame.Level)) throw new InvalidOperationException("Nhập tên tầng.");
                    if (string.IsNullOrEmpty(draftFrame.NameTag) || !selectedFrameSnapshot.Attributes.ContainsKey(draftFrame.NameTag))
                        throw new InvalidOperationException("Chọn tag ATT tên bản vẽ hợp lệ.");
                    var columns = draftFrame.Columns.Where(c => c.Use).ToList();
                    if (columns.Count == 0 || columns.Any(c => string.IsNullOrWhiteSpace(c.Label) || c.Label == "Tầng") ||
                        columns.Select(c => c.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Count)
                        throw new InvalidOperationException("Chọn ít nhất 1 cột, tên cột riêng biệt và khác cột Tầng.");
                    if (data.Frames.Any(f => f.Handle == draftFrame.Handle && f.Id != draftFrame.Id))
                        throw new InvalidOperationException("Block khung này đã đăng ký. Chọn dòng hiện có để sửa.");
                    data.Frames.RemoveAll(f => f.Id == draftFrame.Id);
                    SuiteKinds.BindFloor(data,draftFrame);
                    data.Frames.Add(draftFrame);
                    data.Profile=FrameProfile.From(draftFrame);
                    data.Save(doc.Database, tx); tx.Commit();
                    Refresh(doc);
                    panel.SelectSnapshot(selectedFrameSnapshot);
                    ShowResult("Đã ghi khung và setup vào dữ liệu DWG. Save DWG để giữ khi chuyển máy.");
                }
                else
                {
                    panel.SelectSnapshot(selectedFrameSnapshot);
                    ShowResult(selectedFrameSnapshot.Status + " · " + selectedFrameSnapshot.Candidates.Count +
                        " đối tượng; " + selectedFrameSnapshot.Candidates.Count(c => c.Status == "Cần xác nhận") + " cần xác nhận. Quét chỉ đọc dữ liệu.");
                }
            }
        }
        internal static void MarkRegistrations(FrameSnapshot snapshot,FrameDocument data)
        {
            foreach(var candidate in snapshot.Candidates)
            {
                var source=data.Registrations.SingleOrDefault(r=>r.Handle==candidate.Handle);
                candidate.Registered=source==null?"Chưa đăng ký":source.FrameId==snapshot.Frame.Id?"Đã đăng ký":"Thuộc khung khác";
            }
        }
        [CommandMethod("QAARCHOPEN")]
        public void Open()
        {
            if (palette == null)
            {
                panel = new SuitePanel(Request);
                palette = new PaletteSet("Architectural Suite", new Guid("002a696e-a562-4f31-80f6-7a702e488165"))
                {
                    MinimumSize = new System.Drawing.Size(760, 540),
                    Size = new System.Drawing.Size(1240, 760)
                };
                var container=new System.Windows.Controls.Border{Padding=new System.Windows.Thickness(8,6,18,16),
                    Child=panel,ClipToBounds=true,Background=(System.Windows.Media.Brush)panel.Resources["ToolBackground"]};
                palette.AddVisual("Bảng điều khiển", container, true);
                EnsureEvents();
            }
            palette.Visible = true;
            if (App.DocumentManager.MdiActiveDocument != null)
            {
                try { Refresh(App.DocumentManager.MdiActiveDocument); ShowResult("Chọn khung CAD hoặc chọn dòng đã lưu để tiếp tục."); }
                catch (System.Exception error) { ShowResult(error.Message); }
            }
        }
    }
}
