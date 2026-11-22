using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AICad.Contracts;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Autodesk.AutoCAD.DatabaseServices;
using App = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace Arch.Suite.Host
{
    public class Host : ICadManagedAddin, IExtensionApplication
    {
        private static PaletteSet palette;
        private static TextBlock result, documentName;
        private static StackPanel actions;
        private static ListBox objects;
        private static ListBox candidates;
        internal static void ShowCandidates(List<Candidates.Candidate> rows){if(candidates!=null)candidates.ItemsSource=rows;}
        private static TextBox filter;
        private static Part[] parts = Array.Empty<Part>();
        private static Document observing;
        private static HashSet<string> tracked=new HashSet<string>();
        private static HashSet<string> changed=new HashSet<string>();
        private static void Observe(Document doc)
        {
            if(observing==doc)return;
            if(observing!=null){
                observing.Database.ObjectModified-=Modified;observing.Database.ObjectErased-=Erased;
                observing.CommandEnded-=Ended;observing.ImpliedSelectionChanged-=Selected;
            }
            changed.Clear();tracked.Clear();observing=doc;
            if(doc!=null){
                doc.Database.ObjectModified+=Modified;doc.Database.ObjectErased+=Erased;
                doc.CommandEnded+=Ended;doc.ImpliedSelectionChanged+=Selected;
            }
        }
        private static void Modified(object sender,ObjectEventArgs args){Track(args.DBObject);}
        private static void Erased(object sender,ObjectErasedEventArgs args){Track(args.DBObject);}
        private static void Track(DBObject obj)
        {
            if(pending==null && obj is Entity && tracked.Contains(obj.Handle.ToString()))changed.Add(obj.Handle.ToString());
        }
        private static void Selected(object sender,EventArgs args)
        {
            if(pending!=null || objects==null || observing==null)return;
            var selection=observing.Editor.SelectImplied();
            if(selection.Status!=Autodesk.AutoCAD.EditorInput.PromptStatus.OK || selection.Value.Count!=1)return;
            var handle=selection.Value.GetObjectIds()[0].Handle.ToString();
            var part=parts.FirstOrDefault(p=>p.Handles.Contains(handle));
            if(part!=null){filter.Clear();objects.SelectedItem=part;objects.ScrollIntoView(part);}
        }
        private static void Ended(object sender,CommandEventArgs args)
        {
            if(pending!=null || observing==null)return;
            bool undo=new[]{"U","UNDO","REDO","MREDO"}.Contains(args.GlobalCommandName,StringComparer.OrdinalIgnoreCase);
            if(changed.Count==0 && !undo)return;
            var handles=string.Join(", ",changed.Take(12));changed.Clear();Refresh(observing);
            ShowResult("Bản vẽ đã thay đổi. Xem trước để kiểm tra mặt bằng và thống kê.\n"+handles);
        }
        internal static void Refresh(Document doc)
        {
            if(objects==null)return;
            Observe(doc);
            using(var tx=doc.Database.TransactionManager.StartOpenCloseTransaction()){
                var model=Model.Load(tx,doc.Database);
                parts=model.Parts.OrderBy(p=>p.Level).ThenBy(p=>p.Code).ToArray();
                tracked=new HashSet<string>(parts.SelectMany(p=>p.Handles).Concat(model.Outputs.Values.Concat(model.Tables.Values).Concat(model.Details.Values).Select(v=>v.Handle)));
            }
            Filter();
        }
        private static void Filter()
        {
            objects.ItemsSource=parts.Where(p=>(p.Level+" "+p.Code).IndexOf(filter.Text,StringComparison.CurrentCultureIgnoreCase)>=0).ToList();
        }


        private static string pending;
        private static readonly Brush Background = BrushOf(29,37,48);
        private static readonly Brush Muted = BrushOf(174,193,212);
        private static SolidColorBrush BrushOf(byte r,byte g,byte b) => new SolidColorBrush(Color.FromRgb(r,g,b));
        public IReadOnlyList<CadRibbonPin> ManagedCommands => new[] {
            new CadRibbonPin { Command = "QAARCHOPEN", Label = "Architectural Suite" }
        };
        internal static void ShowResult(string message) { if(result!=null)result.Text=message; }
        public void Initialize() { }
        public void InitializeManaged(Action<string> callback) {  }
        public void Terminate()
        {
            App.DocumentManager.DocumentActivated-=Activated;
            App.DocumentManager.DocumentToBeDestroyed-=Closing;
            Observe(null);candidates=null;
            palette?.Dispose();palette=null;result=null;actions=null;objects=null;filter=null;parts=Array.Empty<Part>();documentName=null;pending=null;
        }
        private static void Activated(object sender,DocumentCollectionEventArgs args)
        {
            if(documentName!=null)documentName.Text=Path.GetFileName(args.Document?.Name);
            if(args.Document!=null)Refresh(args.Document);
            ShowCandidates(new List<Candidates.Candidate>());
            ShowResult("Chọn chức năng để xem kết quả của bản vẽ này.");
        }
        private static void Closing(object sender,DocumentCollectionEventArgs args)
        {
            if(observing!=args.Document)return;
            Observe(null);parts=Array.Empty<Part>();Filter();ShowCandidates(new List<Candidates.Candidate>());
            documentName.Text="";ShowResult("Mở bản vẽ để tiếp tục.");
        }
        private static TextBlock Label(string text,Brush brush,double size) => new TextBlock {
            Text=text,Foreground=brush,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,4)
        };
        private static void Group(string title,params string[] pairs)
        {
            var heading=Label(title,Muted,12);heading.FontWeight=FontWeights.SemiBold;
            actions.Children.Add(heading);
            var grid=new UniformGrid{Columns=2};
            grid.SizeChanged+=(s,e)=>grid.Columns=Math.Max(1,Math.Min(pairs.Length/2,(int)(e.NewSize.Width/128)));
            for(int i=0;i<pairs.Length;i+=2){
                var key=pairs[i];var button=new Button{Content=pairs[i+1],MinHeight=30,Margin=new Thickness(0,0,4,4),Padding=new Thickness(4,2,4,2)};
                button.Click+=(s,e)=>Request(key);grid.Children.Add(button);
            }
            actions.Children.Add(grid);
        }
        private static async void Request(string key)
        {
            if(pending!=null)return;
            var doc=App.DocumentManager.MdiActiveDocument;
            if(doc==null){ShowResult("Mở DWG để tiếp tục.");return;}
            if(Convert.ToInt32(App.GetSystemVariable("CMDACTIVE"))!=0){ShowResult("Kết thúc lệnh CAD hiện tại rồi thử lại.");return;}
            pending=key;actions.IsEnabled=false;
            try {
                await App.DocumentManager.ExecuteInCommandContextAsync(_=>{
                    try {
                        if(doc!=App.DocumentManager.MdiActiveDocument)throw new InvalidOperationException("Bản vẽ đã đổi. Chọn lại thao tác.");
                        using(doc.LockDocument()){
                            if(key=="scan")new Inventory().Scan();
                            else Workspace.Run(doc,key);
                        }
                        Refresh(doc);
                    }
                    catch(OperationCanceledException){ShowResult("Đã hủy thao tác.");}
                    catch(System.Exception ex){ShowResult(ex.Message);doc.Editor.WriteMessage("\n"+ex.Message);}
                    return System.Threading.Tasks.Task.CompletedTask;
                },null);
            }
            catch(System.Exception ex){ShowResult(ex.Message);}
            finally{pending=null;if(actions!=null)actions.IsEnabled=true;}
        }
        [CommandMethod("QAARCHOPEN")]
        public void Open()
        {
            if(palette==null){
                actions=new StackPanel();
                documentName=Label(Path.GetFileName(App.DocumentManager.MdiActiveDocument?.Name),Brushes.White,15);
                documentName.FontWeight=FontWeights.SemiBold;
                Group("BẢN VẼ","region","Chọn mặt bằng nguồn","scan","Kiểm kê · JSON");
                Group("THƯ VIỆN & LỊCH SỬ","layers","Bộ tài nguyên CAD","audit","Nhật ký thay đổi","index","Dựng lại chỉ mục","report","Báo cáo mô hình");
                Group("CẤU KIỆN","wall","Đăng ký tường","door","Đăng ký cửa","inspect","Tra quan hệ","edit","Sửa kích thước","repair","Xử lý xung đột","stats","Kiểm tra dữ liệu");
                Group("MẶT BẰNG & THỐNG KÊ","layout","Đặt hai mặt bằng","preview","Xem trước","apply","Duyệt cập nhật","csv","Xuất bảng CSV");
                Group("BẢNG TRONG DWG","walltable","Thống kê tường","doortable","Thống kê cửa");
                result=Label("Chọn hai Line song song để đăng ký tường. Chọn block cửa và tường chủ để đăng ký cửa.",Brushes.White,13);
                var body=new StackPanel{Margin=new Thickness(10,6,8,8)};
                objects=new ListBox{Height=95,DisplayMemberPath="Caption"};
                filter=new TextBox{MinHeight=24,ToolTip="Lọc theo mã hoặc tầng"};
                filter.TextChanged+=(s,e)=>Filter();
                objects.SelectionChanged+=(s,e)=>{var p=objects.SelectedItem as Part;if(p!=null)ShowResult(p.Caption+"\nGUID "+p.Id+"\nCao "+p.Height+" mm");};
                var locate=new Button{Content="Định vị cấu kiện đã chọn",MinHeight=28};
                locate.Click+=(s,e)=>{var p=objects.SelectedItem as Part;if(p!=null)Request("zoom/"+p.Id);else ShowResult("Chọn một cấu kiện trong danh sách.");};
                var remove=new Button{Content="Gỡ đăng ký",MinHeight=28};
                remove.Click+=(s,e)=>{var p=objects.SelectedItem as Part;if(p!=null)Request("remove/"+p.Id);else ShowResult("Chọn cấu kiện cần gỡ.");};
                var row=new UniformGrid{Columns=2};row.Children.Add(locate);row.Children.Add(remove);
                candidates=new ListBox{MaxHeight=120,DisplayMemberPath="Caption"};
                candidates.SelectionChanged+=(s,e)=>{var item=candidates.SelectedItem as Candidates.Candidate;if(item!=null)ShowResult(item.Evidence);};
                var find=new Button{Content="Tìm trong mặt bằng",MinHeight=28};find.Click+=(s,e)=>Request("candidates");
                var accept=new Button{Content="Đăng ký mục chọn",MinHeight=28};accept.Click+=(s,e)=>{var item=candidates.SelectedItem as Candidates.Candidate;if(item!=null)Request("accept/"+string.Join(",",item.Handles));else ShowResult("Chọn một ứng viên trước.");};
                var buttons=new UniformGrid{Columns=2};buttons.Children.Add(find);buttons.Children.Add(accept);
                var review=new StackPanel();review.Children.Add(buttons);review.Children.Add(candidates);
                actions.Children.Add(new Expander{Header="Duyệt gợi ý tường / cửa",Foreground=Brushes.White,Content=review});
                body.Children.Add(documentName);body.Children.Add(actions);body.Children.Add(Label("TÌM THEO MÃ / TẦNG",Muted,12));body.Children.Add(filter);body.Children.Add(objects);body.Children.Add(row);body.Children.Add(Label("KẾT QUẢ",Muted,12));body.Children.Add(result);
                palette=new PaletteSet("Architectural Suite",new Guid("002a696e-a562-4f31-80f6-7a702e488165"));
                palette.AddVisual("Bảng điều khiển",new ScrollViewer{Background=Background,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=body});
                App.DocumentManager.DocumentActivated+=Activated;
                App.DocumentManager.DocumentToBeDestroyed+=Closing;
            }
            palette.Visible=true;
            if(App.DocumentManager.MdiActiveDocument!=null)Refresh(App.DocumentManager.MdiActiveDocument);
        }
    }
}

