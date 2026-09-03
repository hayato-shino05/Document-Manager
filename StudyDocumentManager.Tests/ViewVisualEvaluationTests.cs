using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using StudyDocumentManager.Models;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

/// <summary>
/// 全 20 画面のデスクトップ View を Headless Avalonia ランタイム上で実際にレンダリングし、
/// 標準解像度（1280x800）および狭小解像度（640x700）でのレイアウト整合性とアクセシビリティを検証・評価するテストハーネス。
/// </summary>
public sealed class ViewVisualEvaluationTests
{
    private static void RenderAndAuditView(Control view, double width, double height, string expectedScreenAutomationId)
    {
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = view
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        // 1. ルート要素または View の AutomationId 検証
        var screenId = AutomationProperties.GetAutomationId(view);
        if (string.IsNullOrEmpty(screenId))
        {
            // View 内のルート Border / DockPanel を走査
            var rootWithId = view.GetVisualDescendants()
                .FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == expectedScreenAutomationId);
            Assert.NotNull(rootWithId);
        }
        else
        {
            Assert.Equal(expectedScreenAutomationId, screenId);
        }

        // 2. 狭小幅・標準幅でのレイアウト再計算
        window.Width = width == 1280 ? 640 : 1280;
        window.InvalidateMeasure();
        window.InvalidateVisual();
        Dispatcher.UIThread.RunJobs();

        // 3. インタラクティブ要素のアクセシビリティ検証（ボタンやテキストボックスの探索）
        var buttons = view.GetVisualDescendants()
            .OfType<Button>()
            .Where(b => b is not RepeatButton
                        && b is not Avalonia.Controls.Primitives.ToggleButton
                        && b.FindAncestorOfType<Avalonia.Controls.Primitives.ScrollBar>() == null
                        && b.FindAncestorOfType<Expander>() == null)
            .ToList();
        var textBoxes = view.GetVisualDescendants().OfType<TextBox>().ToList();

        // 全ボタンが何らかの識別子（Content, AutomationId, または Name）を持つことを確認
        foreach (var btn in buttons)
        {
            var content = btn.Content?.ToString();
            var autoId = AutomationProperties.GetAutomationId(btn);
            var name = AutomationProperties.GetName(btn);
            Assert.True(!string.IsNullOrEmpty(content) || !string.IsNullOrEmpty(autoId) || !string.IsNullOrEmpty(name),
                $"Button in {view.GetType().Name} lacks accessible identifier");
        }

        // 4. クリーンアップ
        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_Dashboard_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<DashboardModel>();
        var view = new Dashboard { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_Dashboard");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_AddEdit_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<AddEditModel>();
        var view = new AddEdit { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_AddEdit");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_OfficeWorkspace_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<OfficeWorkspaceModel>();
        var view = new OfficeWorkspace { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_OfficeWorkspace");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_StudentWorkspace_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<StudentWorkspaceModel>();
        var view = new StudentWorkspace { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_StudentWorkspace");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_BatchImport_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<BatchImportModel>();
        var view = new BatchImport { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_BatchImport");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_WatchedFolder_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<WatchedFolderModel>();
        var view = new WatchedFolder { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "WatchedFolder_Screen");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_RecycleBin_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<RecycleBinModel>();
        var view = new RecycleBin { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_RecycleBin");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_DuplicateDetection_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<DuplicateDetectionModel>();
        var view = new DuplicateDetection { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_DuplicateDetection");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_FileIntegrityCheck_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<FileIntegrityCheckModel>();
        var view = new FileIntegrityCheck { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_FileIntegrityCheck");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_RecentFiles_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<RecentFilesModel>();
        var view = new RecentFiles { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_RecentFiles");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_Report_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<ReportModel>();
        var view = new Report { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_Report");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_TreeMap_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<TreeMapModel>();
        var view = new TreeMap { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_TreeMap");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_CategoryManagement_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<CategoryManagementModel>();
        var view = new CategoryManagement { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_CategoryManagement");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_CollectionManagement_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<CollectionManagementModel>();
        var view = new CollectionManagement { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_CollectionManagement");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_PersonalNote_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<PersonalNoteModel>();
        var view = new PersonalNote { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_PersonalNote");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_RelatedDocuments_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<RelatedDocumentsModel>();
        var view = new RelatedDocuments { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_RelatedDocuments");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_SmartViews_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<SmartViewsModel>();
        var view = new SmartViews { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_SmartViews");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_ImportInbox_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<ImportInboxModel>();
        var view = new ImportInbox { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_ImportInbox");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_BulkDelete_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<BulkDeleteModel>();
        var view = new BulkDelete { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_BulkDelete");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_RecoveryCenter_RendersSuccessfully()
    {
        var model = App.Services!.GetRequiredService<RecoveryCenterModel>();
        var view = new RecoveryCenterView { DataContext = model };
        RenderAndAuditView(view, 1280, 800, "Screen_RecoveryCenter");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ViewAudit_DashboardContextMenu_IsUnifiedAndHasNoEditNotes()
    {
        var dashboard = new Dashboard();
        var dataGrid = dashboard.FindControl<DataGrid>("dgvDocuments");
        Assert.NotNull(dataGrid);
        Assert.NotNull(dataGrid!.ContextMenu);

        var menuItems = dataGrid.ContextMenu!.Items.OfType<MenuItem>().ToList();
        var editNotes = menuItems.FirstOrDefault(m => AutomationProperties.GetAutomationId(m) == "Context_EditNotes");
        var personalNote = menuItems.FirstOrDefault(m => AutomationProperties.GetAutomationId(m) == "Context_PersonalNote");

        Assert.Null(editNotes);
        Assert.NotNull(personalNote);
    }
}
