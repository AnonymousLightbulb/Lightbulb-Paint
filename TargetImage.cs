using Godot;
using System;
using System.Collections.Generic;
using Color = System.Drawing.Color;
using System.Linq;

public partial class TargetImage : Sprite2D
{
    public Vector2I Size = new(15, 15);
    public struct ImageData(Vector2I Dimesions, Vector2I Start)
    {
        public struct DataLayer()
        {
            public enum BlendMode
            {
                Overlay,
                OverwriteOpacity,
            }
            public Dictionary<Vector2I, Color> Pixels = [];
            public byte Opacity;
            public BlendMode Type;
            public bool Hidden;
        }
        public DataLayer Overlay;
        public HashSet<Vector2I> UpdatedOverlay = [];
        public HashSet<Vector2I> UpdatedOverlay2 = [];
        public bool Overlay2;
        public List<DataLayer> Layers = [];
        public Vector2I TopLeft = Start;
        public Vector2I BottomRight = Start + Dimesions - new Vector2I(1, 1);
        public HashSet<Vector2I> UpdatedPixels = [];
        public List<HistoryAction> History = [];
        public List<HistoryAction> RedoActions = [];
        public readonly Vector2I GetSize()
        {
            return new(Mathf.Abs(TopLeft.X) + Mathf.Abs(BottomRight.X) + 1, Mathf.Abs(TopLeft.Y) + Mathf.Abs(BottomRight.Y) + 1);
        }
        public readonly void Validate(Color Fill)
        {
            for (int Layer = 0; Layer < Layers.Count; Layer++)
            {
                for (int PosX = TopLeft.X; PosX <= BottomRight.X; PosX++)
                {
                    for (int PosY = TopLeft.Y; PosY <= BottomRight.Y; PosY++)
                    {
                        if (!Layers[Layer].Pixels.ContainsKey(new(PosX, PosY)))
                        {
                            Layers[Layer].Pixels.Add(new(PosX, PosY), Fill);
                        }
                    }
                }
            }
        }
        public readonly void AddLayer(int Layer, DataLayer ToAdd)
        {
            if (Layer >= Layers.Count)
            {
                Layers.Add(ToAdd);
            }
            else
            {
                Layers.Insert(Layer, ToAdd);
            }
            Validate(Color.FromArgb(0, 0, 0, 0));
        }
        public readonly void RemoveLayer(int Layer)
        {
            if (Layers.Count > 1)
            {
                Layer = Mathf.Clamp(Layer, 0, Layers.Count - 1);
                Layers.RemoveAt(Layer);
                for (int PosX = TopLeft.X; PosX <= BottomRight.X; PosX++)
                {
                    for (int PosY = TopLeft.Y; PosY <= BottomRight.Y; PosY++)
                    {
                        UpdatedPixels.Add(new(PosX, PosY));
                    }
                }
            }
        }
    }
    public struct FillCommand(Vector2I Posit, Color Replaceable)
    {
        public Vector2I Pos = Posit;
        public Color ToReplace = Replaceable;
    }
    public class HistoryAction
    {
        public int Layer;
    }
    public class DrawHistory : HistoryAction
    {
        public Dictionary<Vector2I, Color> Data = new();
    }
    public class LayerHistory : HistoryAction
    {
        public ImageData.DataLayer Data;

        public LayerAction WhatWasDone;

        public enum LayerAction
        {
            Added,
            Removed,
            MovedUp,
            MovedDown,
            Hide,
        }
    }
    public ButtonGroup SelectedShape;
    public Image DisplayImage;
    public ImageData EditableImage;
    [Export] public NinePatchRect BG;
    [Export] public bool DrewLastFrame;
    [Export] public bool FilledLastFrame;
    [Export] public Vector2 PosLastFrame;
    public Color SelectedColor;
    [Export] public float ZoomIn = 1;
    [Export] public Godot.Range DrawSize;
    [Export] public ButtonGroup Shape;
    public List<FillCommand> QueuedFills = [];
    public List<Vector2I> Fills;
    public int DoneFills;
    [Export] double FillTIme;
    public bool CheckNextFrameTime;
    public DrawHistory NextHistoryAction = new();

    [Export] public Godot.Range R;
    [Export] public Godot.Range G;
    [Export] public Godot.Range B;
    [Export] public Godot.Range A;
    [Export] public LineEdit Hex;
    [Export] public Godot.Range H;
    [Export] public Godot.Range S;
    [Export] public Godot.Range V;
    [Export] public ColorRect ColorDisplay;
    public bool ColorChanged;
    //[Export] public Godot.Range LayerSelector;
    [Export] public Control[] UiBounds;
    [Export] public MenuButton FileMenuButton;
    [Export] public FileDialog ExportMenu;
    [Export] public FileDialog LoadMenu;
    [Export] public PanelContainer New;
    [Export] public Button Erase;
    [Export] public Button Picker;
    [Export] public ItemList LayerList;

    [Export] public LineEdit NewX;
    [Export] public LineEdit NewY;
    [Export] Vector2 ClickOffset;

    public List<Image> LayerThumbnails = [];
    public List<Texture2D> LayerTextures = [];

    public void FileMenu(long Button)
    {
        switch (Button)
        {
            case 0:
                ExportMenu.Show();
                break;
            case 1:
                LoadMenu.Show();
                break;
            case 2:
                New.Show();
                break;
            default:
                break;
        }
    }
    public void Save(string FilePath)
    {
        if (FilePath.Split('.').Last().ToLower() == "png")
        {
            DisplayImage.SavePng(FilePath);
        }
        else if (FilePath.Split('.').Last().ToLower() == "jpg")
        {
            DisplayImage.SaveJpg(FilePath);
        }
        else if (FilePath.Split('.').Last().ToLower() == "webp")
        {
            DisplayImage.SaveWebp(FilePath);
        }
        else if (FilePath.Split('.').Last().ToLower() == "dds")
        {
            DisplayImage.SaveDds(FilePath);
        }
        else if (FilePath.Split('.').Last().ToLower() == "exr")
        {
            DisplayImage.SaveExr(FilePath);
        }
        // DisplayImage.Save
    }
    public void Load(string FilePath)
    {
        if (FilePath.Split('.').Last().ToLower() == "png" || FilePath.Split('.').Last().ToLower() == "jpg" || FilePath.Split('.').Last().ToLower() == "webp" || FilePath.Split('.').Last().ToLower() == "dds" || FilePath.Split('.').Last().ToLower() == "exr")
        {
            Image ToLoad = Image.LoadFromFile(FilePath);
            Size = ToLoad.GetSize();
            GenerateBlank();
            for (int X = 0; X < ToLoad.GetSize().X; X++)
            {
                for (int Y = 0; Y < ToLoad.GetSize().Y; Y++)
                {
                    EditableImage.Layers[0].Pixels[new(X, Y)] = Color.FromArgb(ToLoad.GetPixel(X, Y).A8, ToLoad.GetPixel(X, Y).R8, ToLoad.GetPixel(X, Y).G8, ToLoad.GetPixel(X, Y).B8);
                    EditableImage.UpdatedPixels.Add(new(X, Y));
                }
            }
            GD.Print(EditableImage.UpdatedPixels.Count);
            RefreshImage();
            GD.Print(EditableImage.UpdatedPixels.Count);
        }
    }
    public void GenerateBlank()
    {
        LayerList.Clear();
        EditableImage = new(Size, Vector2I.Zero);
        EditableImage.AddLayer(0, new());
        EditableImage.Validate(Color.FromArgb(0, 0, 0, 0));
        DisplayImage = Image.CreateEmpty(Size.X, Size.Y, false, Image.Format.Rgba8);
        ResizeImage();
        Texture = ImageTexture.CreateFromImage(DisplayImage);
        (BG.GetParent() as ColorRect).Size = Texture.GetSize() * Scale;
        LayerThumbnails = [];
        LayerThumbnails.Add(Image.CreateEmpty(Size.X, Size.Y, false, Image.Format.Rgba8));
        RefreshImage();

        Input.ActionPress("Maximize");
    }
    public void RefreshImage()
    {
        Texture2D asdf = Texture;
        foreach (Vector2I item in EditableImage.UpdatedPixels)
        {
            Godot.Color Under = new(0, 0, 0, 0);
            for (int Layer = 0; Layer < EditableImage.Layers.Count; Layer++)
            {
                if (EditableImage.Layers[Layer].Hidden == false)
                {
                    Godot.Color Over = new((float)EditableImage.Layers[Layer].Pixels[item].R / 255f, (float)EditableImage.Layers[Layer].Pixels[item].G / 255f, (float)EditableImage.Layers[Layer].Pixels[item].B / 255f, (float)EditableImage.Layers[Layer].Pixels[item].A / 255f);
                    if (!Mathf.IsEqualApprox(Over.A, 0))
                    {
                        float Alpha = Over.A + Under.A * (1 - Over.A);
                        float Red = (Over.R * Over.A + Under.R * Under.A * (1 - Over.A)) / Alpha;
                        float Green = (Over.G * Over.A + Under.G * Under.A * (1 - Over.A)) / Alpha;
                        float Blue = (Over.B * Over.A + Under.B * Under.A * (1 - Over.A)) / Alpha;
                        Under = new(Red, Green, Blue, Alpha);
                    }
                }
                LayerThumbnails[Layer].SetPixelv(item, new(EditableImage.Layers[Layer].Pixels[item].R / 255f, EditableImage.Layers[Layer].Pixels[item].G / 255f, EditableImage.Layers[Layer].Pixels[item].B / 255f, EditableImage.Layers[Layer].Pixels[item].A / 255f));
            }
            DisplayImage.SetPixelv(item, Under);
        }
        int MaxLayer = EditableImage.Layers.Count;
        int CurrentLayer = 0;
        if (LayerList.GetSelectedItems().Count() > 0)
        {
            CurrentLayer = LayerList.GetSelectedItems()[0];
        }
        LayerList.Clear();
        LayerTextures = [];
        for (int Layer = 0; Layer < MaxLayer; Layer++)
        {
            LayerTextures.Add(ImageTexture.CreateFromImage(LayerThumbnails[Layer]));
        }
        for (int Layer = MaxLayer - 1; Layer >= 0; Layer--)
        {
            LayerList.AddItem($"Layer {Layer}", LayerTextures[Layer]);
        }
        LayerList.Select(Mathf.Clamp(CurrentLayer, 0, EditableImage.Layers.Count() - 1));
        EditableImage.UpdatedPixels = [];
        Texture = ImageTexture.CreateFromImage(DisplayImage);
        asdf.Dispose();
    }
    public void ResizeImage()
    {
        DisplayImage.Crop(Size.X, Size.Y);
    }
    public void SliderChanger(float idgaf)
    {
        if (ColorChanged == false)
        {
            ColorChanged = true;
            Hex.Text = Convert.ToHexString([(byte)Mathf.RoundToInt(R.Value), (byte)Mathf.RoundToInt(G.Value), (byte)Mathf.RoundToInt(B.Value), (byte)Mathf.RoundToInt(A.Value)]);
            Godot.Color asdf = new((float)R.Value / 255, (float)G.Value / 255, (float)B.Value / 255);
            H.Value = asdf.H * 360;
            S.Value = asdf.S * 100;
            V.Value = asdf.V * 100;
            // GD.Print(asdf.H + ", " + H.Value);
            // GD.Print(asdf.S + ", " + S.Value);
            // GD.Print(asdf.V + ", " + V.Value);
        }
    }
    public void HexChanger(string Asdf)
    {
        if (ColorChanged == false)
        {
            ColorChanged = true;
            byte[] bytes = Convert.FromHexString(Asdf);
            R.Value = bytes[0];
            G.Value = bytes[1];
            B.Value = bytes[2];
            A.Value = bytes[3];
            Godot.Color asdf = new((float)R.Value, (float)G.Value, (float)B.Value);
            H.Value = asdf.H * 360;
            S.Value = asdf.S * 100;
            V.Value = asdf.V * 100;
        }
    }
    public void HsvChange(float idgaf)
    {
        if (ColorChanged == false)
        {
            ColorChanged = true;
            R.Value = Mathf.Round(Godot.Color.FromHsv((float)H.Value / 360, (float)S.Value / 100, (float)V.Value / 100).R * 255);
            G.Value = Mathf.Round(Godot.Color.FromHsv((float)H.Value / 360, (float)S.Value / 100, (float)V.Value / 100).G * 255);
            B.Value = Mathf.Round(Godot.Color.FromHsv((float)H.Value / 360, (float)S.Value / 100, (float)V.Value / 100).B * 255);
            Hex.Text = Convert.ToHexString([(byte)Mathf.RoundToInt(R.Value), (byte)Mathf.RoundToInt(G.Value), (byte)Mathf.RoundToInt(B.Value), (byte)Mathf.RoundToInt(A.Value)]);
            new Color();
        }
    }

    public override void _Ready()
    {
        FileMenuButton.GetPopup().IdPressed += FileMenu;
        // byte[] PixelsA = [255, 128, 128, 255, 0, 128, 128, 255];
        // byte[] PixelsB = [128, 128, 255, 255, 128, 128, 0, 255];
        // List<byte> FinalPixels = [];
        // for (int Y = 0; Y < 512; Y++)
        // {
        //     for (int X = 0; X < 256; X++)
        //     {
        //         if (Y % 2 == 0)
        //         {
        //             for (int Z = 0; Z < PixelsA.Length; Z++)
        //             {
        //                 FinalPixels.Add(PixelsA[Z]);
        //             }
        //         }
        //         else if (Y % 2 == 1)
        //         {
        //             for (int Z = 0; Z < PixelsA.Length; Z++)
        //             {
        //                 FinalPixels.Add(PixelsB[Z]);
        //             }
        //         }
        //     }
        // }
        GenerateBlank();
        base._Ready();
    }

    public void NewFromSize()
    {
        if (int.Parse(NewX.Text) > 0 && int.Parse(NewY.Text) > 0)
        {
            Size = new(int.Parse(NewX.Text), int.Parse(NewY.Text));
            GenerateBlank();
            GD.Print($"{EditableImage.TopLeft}, {EditableImage.BottomRight}");
            New.Hide();
        }
    }

    public override void _Process(double delta)
    {
        int BiggerAxis = Size.X;
        if (Size.Y > Size.X)
        {
            BiggerAxis = Size.Y;
        }
        LayerList.IconScale = 48f / BiggerAxis;
        // GD.Print($"{ColorDisplay.Color.H}, {ColorDisplay.Color.S}, {ColorDisplay.Color.V}");
        ColorChanged = false;
        SelectedColor = Color.FromArgb((byte)Mathf.RoundToInt(A.Value), (byte)Mathf.RoundToInt(R.Value), (byte)Mathf.RoundToInt(G.Value), (byte)Mathf.RoundToInt(B.Value));
        DoneFills = 0;
        if (Input.IsActionJustPressed("Escape"))
        {
            GetViewport().GuiReleaseFocus();
        }
        // if (Input.IsActionPressed("Center"))
        // {
        //     Position = GetViewport().GetVisibleRect().Size / 2 - new Godot.Vector2(100, 0);
        // }
        if (Input.IsActionJustPressed("Maximize"))
        {
            float MaximizeX = (GetViewport().GetVisibleRect().Size.X - 254) / Texture.GetSize().X;
            float MaximizeY = (GetViewport().GetVisibleRect().Size.Y - 38) / Texture.GetSize().Y;
            if (MaximizeX < MaximizeY)
            {
                ZoomIn = MaximizeX;
            }
            else
            {
                ZoomIn = MaximizeY;
            }
            (GetParent() as Node2D).Position = GetViewport().GetVisibleRect().Size / 2 - new Vector2(127, -19);
            Input.ActionRelease("Maximize");
        }
        float Multiplier = 1;
        if (Size.X > Size.Y)
        {
            Multiplier = 1024 / Size.X;
        }
        else
        {
            Multiplier = 1024 / Size.Y;
        }
        ZoomIn = Mathf.Clamp(ZoomIn + (Input.GetAxis("Zoom Out", "Zoom In") * 10 * (float)delta * Multiplier), 0.05f, float.PositiveInfinity);
        Scale = new(ZoomIn, ZoomIn);
        // Position += Input.GetVector("ui_right", "ui_left", "ui_down", "ui_up") * (float)delta * 320 * ZoomIn;

        if (Input.IsActionPressed("Draw"))
        {
            if (Picker.ButtonPressed == false)
            {
                if (Shape.GetPressedButton().Name == "Square" || Shape.GetPressedButton().Name == "Circle")
                {
                    // DrawLine(GetLocalMousePosition() + (DisplayImage.GetSize() / 2), PosLastFrame);
                    if (DrewLastFrame == true)
                    {
                        DrawLine(GetLocalMousePosition() + new Vector2((float)EditableImage.GetSize().X / 2 + EditableImage.TopLeft.X, (float)EditableImage.GetSize().Y / 2 + EditableImage.TopLeft.Y) + ClickOffset, PosLastFrame);
                    }
                    else
                    {
                        DrawLine(GetLocalMousePosition() + new Vector2((float)EditableImage.GetSize().X / 2 + EditableImage.TopLeft.X, (float)EditableImage.GetSize().Y / 2 + EditableImage.TopLeft.Y) + ClickOffset, GetLocalMousePosition() + new Vector2((float)EditableImage.GetSize().X / 2 + EditableImage.TopLeft.X, (float)EditableImage.GetSize().Y / 2 + EditableImage.TopLeft.Y) + ClickOffset);
                    }
                }
                else if (DrewLastFrame == false && Shape.GetPressedButton().Name == "Fill")
                {
                    if (Mathf.RoundToInt(GetLocalMousePosition().X + (float)EditableImage.GetSize().X / 2 + EditableImage.TopLeft.X) >= EditableImage.TopLeft.X && Mathf.RoundToInt(GetLocalMousePosition().X + (float)EditableImage.GetSize().X / 2 + EditableImage.TopLeft.X) <= EditableImage.BottomRight.X && Mathf.RoundToInt(GetLocalMousePosition().Y + (float)EditableImage.GetSize().Y / 2 + EditableImage.TopLeft.Y) >= EditableImage.TopLeft.Y && Mathf.RoundToInt(GetLocalMousePosition().Y + (float)EditableImage.GetSize().Y / 2 + EditableImage.TopLeft.Y) <= EditableImage.BottomRight.Y)
                    {
                        // Fill(new(new Vector2I(Mathf.RoundToInt(GetLocalMousePosition().X + (DisplayImage.GetSize().X / 2)), Mathf.RoundToInt(GetLocalMousePosition().Y + (DisplayImage.GetSize().Y / 2))), DisplayImage.GetPixel(Mathf.RoundToInt((GetLocalMousePosition() + (DisplayImage.GetSize() / 2)).X), Mathf.RoundToInt((GetLocalMousePosition() + (DisplayImage.GetSize() / 2)).Y))));
                        // Fill(new(new Vector2I(Mathf.RoundToInt(GetLocalMousePosition().X + (float)EditableImage.GetSize().X / 2 + EditableImage.TopLeft.X), Mathf.RoundToInt(GetLocalMousePosition().Y + (float)EditableImage.GetSize().Y / 2 + EditableImage.TopLeft.Y)), DisplayImage.GetPixel(Mathf.RoundToInt((GetLocalMousePosition() + (DisplayImage.GetSize() / 2)).X), Mathf.RoundToInt((GetLocalMousePosition() + (DisplayImage.GetSize() / 2)).Y))));
                        StartFill(new(Mathf.RoundToInt(GetLocalMousePosition().X + (float)EditableImage.GetSize().X / 2 + EditableImage.TopLeft.X), Mathf.RoundToInt(GetLocalMousePosition().Y + (float)EditableImage.GetSize().Y / 2 + EditableImage.TopLeft.Y)));
                    }
                    // Fill(new(new Vector2I(1024, 1024), DisplayImage.GetPixel(Mathf.RoundToInt((GetLocalMousePosition() + (DisplayImage.GetSize() / 2)).X), Mathf.RoundToInt((GetLocalMousePosition() + (DisplayImage.GetSize() / 2)).Y))));
                    // if (Mathf.RoundToInt(GetLocalMousePosition().X + (DisplayImage.GetSize().X / 2)) >= 0 && Mathf.RoundToInt(GetLocalMousePosition().X + (DisplayImage.GetSize().X / 2)) < DisplayImage.GetSize().X && Mathf.RoundToInt(GetLocalMousePosition().Y + (DisplayImage.GetSize().Y / 2)) >= 0 && Mathf.RoundToInt(GetLocalMousePosition().Y + (DisplayImage.GetSize().Y / 2)) < DisplayImage.GetSize().Y)
                    // {
                    //     GD.Print($"{DisplayImage.GetPixel(Mathf.RoundToInt((GetLocalMousePosition() + (DisplayImage.GetSize() / 2)).X), Mathf.RoundToInt((GetLocalMousePosition() + (DisplayImage.GetSize() / 2)).Y))}, {SelectedColor.Color}");
                    // }
                }
                DrewLastFrame = true;
                RefreshImage();
            }
            else
            {
                if (Mathf.RoundToInt(GetLocalMousePosition().X + (float)EditableImage.GetSize().X / 2 + EditableImage.TopLeft.X) >= EditableImage.TopLeft.X && Mathf.RoundToInt(GetLocalMousePosition().X + (float)EditableImage.GetSize().X / 2 + EditableImage.TopLeft.X) <= EditableImage.BottomRight.X && Mathf.RoundToInt(GetLocalMousePosition().Y + (float)EditableImage.GetSize().Y / 2 + EditableImage.TopLeft.Y) >= EditableImage.TopLeft.Y && Mathf.RoundToInt(GetLocalMousePosition().Y + (float)EditableImage.GetSize().Y / 2 + EditableImage.TopLeft.Y) <= EditableImage.BottomRight.Y)
                {
                    R.Value = EditableImage.Layers[(EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]].Pixels[new(Mathf.RoundToInt(GetLocalMousePosition().X + (float)EditableImage.GetSize().X / 2 + EditableImage.TopLeft.X), Mathf.RoundToInt(GetLocalMousePosition().Y + (float)EditableImage.GetSize().Y / 2 + EditableImage.TopLeft.Y))].R;
                    ColorChanged = false;
                    G.Value = EditableImage.Layers[(EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]].Pixels[new(Mathf.RoundToInt(GetLocalMousePosition().X + (float)EditableImage.GetSize().X / 2 + EditableImage.TopLeft.X), Mathf.RoundToInt(GetLocalMousePosition().Y + (float)EditableImage.GetSize().Y / 2 + EditableImage.TopLeft.Y))].G;
                    ColorChanged = false;
                    B.Value = EditableImage.Layers[(EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]].Pixels[new(Mathf.RoundToInt(GetLocalMousePosition().X + (float)EditableImage.GetSize().X / 2 + EditableImage.TopLeft.X), Mathf.RoundToInt(GetLocalMousePosition().Y + (float)EditableImage.GetSize().Y / 2 + EditableImage.TopLeft.Y))].B;
                    ColorChanged = false;
                    A.Value = EditableImage.Layers[(EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]].Pixels[new(Mathf.RoundToInt(GetLocalMousePosition().X + (float)EditableImage.GetSize().X / 2 + EditableImage.TopLeft.X), Mathf.RoundToInt(GetLocalMousePosition().Y + (float)EditableImage.GetSize().Y / 2 + EditableImage.TopLeft.Y))].A;
                }
            }
        }
        else
        {
            DrewLastFrame = false;
        }
        if (Input.IsActionJustReleased("Draw") && NextHistoryAction.Data.Count > 0)
        {
            EditableImage.History.Add((NextHistoryAction));
            NextHistoryAction = new();
        }
        // Color ToReplace;
        // if ()
        while (QueuedFills.Count > 0 && DoneFills <= 131072 / 8)
        {
            // if (!Filled.Contains(QueuedFills[0].Pos))
            // {
            Fill(QueuedFills[0]);
            // }
            // Filled.Add(QueuedFills[0].Pos);
            QueuedFills.RemoveAt(0);
            DoneFills += 1;
        }
        if (DoneFills > 0)
        {
            Texture2D asdf = Texture;
            DrewLastFrame = true;
            RefreshImage();
            Texture = ImageTexture.CreateFromImage(DisplayImage);
            asdf.Dispose();
            FillTIme += delta;
        }
        else if (CheckNextFrameTime)
        {
            FillTIme += delta;
            CheckNextFrameTime = false;
        }
        (BG.GetParent() as ColorRect).GlobalPosition = GlobalPosition - (Texture.GetSize() * Scale / 2);
        (BG.GetParent() as ColorRect).Size = Texture.GetSize() * Scale;
        BG.GlobalPosition = Vector2.Zero;
        BG.Size = GetViewport().GetVisibleRect().Size;
        ((GetParent().GetChild(1) as CollisionShape2D).Shape as RectangleShape2D).Size = Texture.GetSize() * Scale;
        ColorDisplay.Color = new((float)R.Value / 255f, (float)G.Value / 255f, (float)B.Value / 255f, (float)A.Value / 255f);
        base._Process(delta);
    }
    public void DrawLine(Vector2 EndPos, Vector2 CurrentPos)
    {
        Vector2 TargetPosition = EndPos;
        Vector2I RealTargetPixel = new(Mathf.RoundToInt(TargetPosition.X), Mathf.RoundToInt(TargetPosition.Y));
        Vector2I TargetPixel = RealTargetPixel;
        bool Drawing = true;
        while (Drawing == true)
        {
            Vector2 Posit = ToGlobal(new(CurrentPos.X - EditableImage.TopLeft.X - (EditableImage.BottomRight.X / 2), CurrentPos.Y - EditableImage.TopLeft.Y - (EditableImage.BottomRight.Y / 2)));
            bool Blocked = false;
            foreach (Control item in UiBounds)
            {
                if (item.IsVisibleInTree() == true && Posit.X > item.GlobalPosition.X && Posit.X < item.GlobalPosition.X + item.Size.X && Posit.Y > item.GlobalPosition.Y && Posit.Y < item.GlobalPosition.Y + item.Size.Y)
                {
                    Blocked = true;
                    Drawing = false;
                }
            }
            if (Blocked == false)
            {
                if (DrewLastFrame == true)
                {
                    CurrentPos = CurrentPos.MoveToward(TargetPosition, 0.5f);
                    TargetPixel = new(Mathf.RoundToInt(CurrentPos.X), Mathf.RoundToInt(CurrentPos.Y));
                }
                Godot.Collections.Array<Vector2I> Outline = [];
                if (Shape.GetPressedButton().Name == "Square")
                {
                    for (int startX = -(Mathf.RoundToInt(DrawSize.Value) / 2); startX < Mathf.RoundToInt(DrawSize.Value) - (Mathf.RoundToInt(DrawSize.Value) / 2); startX++)
                    {
                        for (int startY = -(Mathf.RoundToInt(DrawSize.Value) / 2); startY < Mathf.RoundToInt(DrawSize.Value) - (Mathf.RoundToInt(DrawSize.Value) / 2); startY++)
                        {
                            DrawPixel(TargetPixel + new Vector2I(startX, startY));
                        }
                    }
                }
                else if (Shape.GetPressedButton().Name == "Circle")
                {
                    for (int startX = -(Mathf.RoundToInt(DrawSize.Value) / 2); startX <= Mathf.RoundToInt(DrawSize.Value) / 2; startX++)
                    {
                        for (int startY = -(Mathf.RoundToInt(DrawSize.Value) / 2); startY <= Mathf.RoundToInt(DrawSize.Value) / 2; startY++)
                        {
                            DrawCirclePixel(TargetPixel + new Vector2I(startX, startY), TargetPixel, false);
                        }
                    }
                }
                if (TargetPixel == RealTargetPixel)
                {
                    Drawing = false;
                }
            }
        }
        PosLastFrame = TargetPosition;
    }
    public void DrawPixel(Vector2I Pos, bool Draw = true)
    {
        if (Pos.X >= EditableImage.TopLeft.X && Pos.X <= EditableImage.BottomRight.X && Pos.Y >= EditableImage.TopLeft.Y && Pos.Y <= EditableImage.BottomRight.Y)
        {
            while (EditableImage.Layers.Count <= (EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0])
            {
                AddLayer();
            }
            NextHistoryAction.Data.TryAdd(Pos, EditableImage.Layers[(EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]].Pixels[Pos]);
            if (Erase.ButtonPressed == false)
            {
                EditableImage.Layers[(EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]].Pixels[Pos] = SelectedColor;
                EditableImage.UpdatedPixels.Add(Pos);
            }
            else
            {
                EditableImage.Layers[(EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]].Pixels[Pos] = Color.Transparent;
                EditableImage.UpdatedPixels.Add(Pos);
            }
            EditableImage.RedoActions = [];
        }
    }
    public void DrawCirclePixel(Vector2I Pos, Vector2I Center, bool Draw = true)
    {
        if (Pos.DistanceTo(Center) <= (float)DrawSize.Value / 2 && Pos.X >= EditableImage.TopLeft.X && Pos.X <= EditableImage.BottomRight.X && Pos.Y >= EditableImage.TopLeft.Y && Pos.Y <= EditableImage.BottomRight.Y)
        {
            while (EditableImage.Layers.Count <= (EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0])
            {
                AddLayer();
            }
            NextHistoryAction.Data.TryAdd(Pos, EditableImage.Layers[(EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]].Pixels[Pos]);
            if (Erase.ButtonPressed == false)
            {
                EditableImage.Layers[(EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]].Pixels[Pos] = SelectedColor;
                EditableImage.UpdatedPixels.Add(Pos);
            }
            else
            {
                EditableImage.Layers[(EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]].Pixels[Pos] = Color.Transparent;
                EditableImage.UpdatedPixels.Add(Pos);
            }
            EditableImage.RedoActions = [];
        }
    }
    public void StartFill(Vector2I Target)
    {
        Fill(new(Target, EditableImage.Layers[(EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]].Pixels[Target]));
    }
    public void NeoFill(FillCommand command)
    {
        List<FillCommand> S = [command];
    }
    public void Fill(FillCommand command)
    {
        if (command.Pos.X >= EditableImage.TopLeft.X && command.Pos.X <= EditableImage.BottomRight.X && command.Pos.Y >= EditableImage.TopLeft.Y && command.Pos.Y <= EditableImage.BottomRight.Y)
        {
            if (SelectedColor == command.ToReplace)
            {
                GD.Print("asdf");
                return;
            }
            if (EditableImage.Layers[(EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]].Pixels[command.Pos] == command.ToReplace || command.ToReplace.A == 0 && EditableImage.Layers[(EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]].Pixels[command.Pos].A == 0)
            {
                EditableImage.RedoActions = [];
                EditableImage.Layers[(EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]].Pixels[command.Pos] = SelectedColor;
                EditableImage.UpdatedPixels.Add(command.Pos);
                for (int i = 0; i < 4; i++)
                {
                    Vector2I DrawOffset = Vector2I.Left;
                    switch (i)
                    {
                        case 1:
                            DrawOffset = Vector2I.Right;
                            break;
                        case 2:
                            DrawOffset = Vector2I.Up;
                            break;
                        case 3:
                            DrawOffset = Vector2I.Down;
                            break;
                    }
                    if (command.Pos.X + DrawOffset.X >= EditableImage.TopLeft.X && command.Pos.X + DrawOffset.X <= EditableImage.BottomRight.X && command.Pos.Y + DrawOffset.Y >= EditableImage.TopLeft.Y && command.Pos.Y + DrawOffset.Y <= EditableImage.BottomRight.Y)
                    {
                        // if (SelectedColor.Color.R8 != command.ToReplace.R8 || SelectedColor.Color.G8 != command.ToReplace.G8 || SelectedColor.Color.B8 != command.ToReplace.B8 || SelectedColor.Color.A8 != command.ToReplace.A8)
                        // {
                        // if (SelectedColor.Color.R8 != DisplayImage.GetPixelv(command.Pos + DrawOffset).R8 || SelectedColor.Color.G8 != DisplayImage.GetPixelv(command.Pos + DrawOffset).G8 || SelectedColor.Color.B8 != DisplayImage.GetPixelv(command.Pos + DrawOffset).B8 || SelectedColor.Color.A8 != DisplayImage.GetPixelv(command.Pos + DrawOffset).A8)
                        // {
                        QueuedFills.Add(new(command.Pos + DrawOffset, command.ToReplace));
                        // }
                        // }
                    }
                }
            }
        }
    }
    public override void _Input(InputEvent @event)
    {
        Vector2 Posit = GetGlobalMousePosition();
        bool Blocked = false;
        foreach (Control item in UiBounds)
        {
            if (item.IsVisibleInTree() == true && Posit.X > item.GlobalPosition.X && Posit.X < item.GlobalPosition.X + item.Size.X && Posit.Y > item.GlobalPosition.Y && Posit.Y < item.GlobalPosition.Y + item.Size.Y)
            {
                Blocked = true;
            }
        }
        if (Blocked == false)
        {
            if (@event is InputEventMouseMotion && Input.IsActionPressed("Move"))
            {
                (GetParent() as Node2D).Position += (@event as InputEventMouseMotion).Relative;
            }
            else if (@event is InputEventMouseButton)
            {
                float Multiplier = 1;
                if (Size.X > Size.Y)
                {
                    Multiplier = 1024 / Size.X;
                }
                else
                {
                    Multiplier = 1024 / Size.Y;
                }
                ZoomIn = Mathf.Clamp(ZoomIn + (Input.GetAxis("Zoom Out", "Zoom In") * 0.05f * Multiplier), 0.05f, float.PositiveInfinity);
            }
        }
        base._Input(@event);
    }

    public void input_event(Node viewport, InputEvent Ievent, int shape_idx)
    {
        // Check if the event is a mouse button press (left click)
        if (Ievent is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
            {
                GD.Print("Sprite was clicked!");
                // Add your custom logic here (e.g., change scene, start animation, etc.)
            }
        }
    }
    public void Undo()
    {
        if (EditableImage.History.Count > 0)
        {
            if (EditableImage.History.Last() is DrawHistory)
            {
                DrawHistory asdf = new();
                asdf.Layer = EditableImage.History.Last().Layer;
                foreach (var item in (EditableImage.History.Last() as DrawHistory).Data)
                {
                    asdf.Data.Add(item.Key, EditableImage.Layers[(EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]].Pixels[item.Key]);
                    EditableImage.Layers[EditableImage.History.Last().Layer].Pixels[item.Key] = item.Value;
                    EditableImage.UpdatedPixels.Add(item.Key);
                }
                RefreshImage();
                EditableImage.History.RemoveAt(EditableImage.History.Count - 1);
                EditableImage.RedoActions.Add(asdf);
            }
        }
    }
    public void Redo()
    {
        if (EditableImage.RedoActions.Count > 0)
        {
            if (EditableImage.RedoActions[0] is DrawHistory)
            {
                DrawHistory asdf = new();
                asdf.Layer = EditableImage.RedoActions[0].Layer;
                foreach (var item in (EditableImage.RedoActions[0] as DrawHistory).Data)
                {
                    asdf.Data.Add(item.Key, EditableImage.Layers[(EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]].Pixels[item.Key]);
                    EditableImage.Layers[EditableImage.RedoActions[0].Layer].Pixels[item.Key] = item.Value;
                    EditableImage.UpdatedPixels.Add(item.Key);
                }
                RefreshImage();
                EditableImage.RedoActions.RemoveAt(0);
                EditableImage.History.Add(asdf);
            }
        }
    }
    public void AddLayer()
    {
        EditableImage.AddLayer(((EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0] + 1), new());
        if ((EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0] >= EditableImage.Layers.Count() - 1)
        {
            LayerThumbnails.Add(Image.CreateEmpty(Size.X, Size.Y, false, Image.Format.Rgba8));
        }
        else
        {
            LayerThumbnails.Insert((EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0], Image.CreateEmpty(Size.X, Size.Y, false, Image.Format.Rgba8));
        }
        RefreshImage();
    }
    public void RemoveLayer()
    {
        if (EditableImage.Layers.Count > 1)
        {
            LayerThumbnails.RemoveAt((EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]);
            EditableImage.RemoveLayer((EditableImage.Layers.Count() - 1) - LayerList.GetSelectedItems()[0]);
            RefreshImage();
        }
    }
}
