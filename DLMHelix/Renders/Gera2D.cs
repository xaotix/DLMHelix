﻿using DLM.helix._3d;
using DLM.helix.Util;
using HelixToolkit.Wpf;
using Poly2Tri.Triangulation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using DLM.cam;
using DLM.vars;

namespace DLM.helix
{
    public class Gera2D
    {
        /*
         adicionar suporte a:
                recortes internos
                soldas
                projeções pontilhadas
                croquis de furos de dxf
                

         */
        public static void Desenho(ReadCAM cam, HelixViewport3D viewPort)
        {
            
            double espessura = 1;
            List<LinesVisual3D> linhas = new List<LinesVisual3D>();
            viewPort.Children.Clear();
            viewPort.Children.Add(Gera3d.Luz());
            ControleCamera.Setar(viewPort, ControleCamera.eCameraViews.Top, 0); ;
            Ponto3d origem = new Ponto3d();
            var cor = Brushes.Black.Color;
            var shape = cam.GetShapeLIV1();
            double ctf = cam.ContraFlecha;

            double offset = 25;
            Ponto3d origem_Liv2 = origem.MoverXY(90, offset + (cam.Faces>2? cam.LIV2_Largura:0));
            Ponto3d origem_Liv3 = origem.MoverXY(90, -cam.LIV1_Largura - cam.LIV3_Largura - offset);



            #region CHAPAS
            if (cam.TipoPerfil == CAM_PERFIL_TIPO.Barra_Chata | cam.TipoPerfil == CAM_PERFIL_TIPO.Chapa | cam.TipoPerfil == CAM_PERFIL_TIPO.Chapa_Xadrez)
            {
                linhas.AddRange(Contorno(espessura, shape, origem, cor, ctf));
            }
            else
            {
                //Liv1
                linhas.AddRange(Contorno(espessura, shape, origem, cor, 0));


                //LIV2
                linhas.AddRange(Contorno(espessura, cam.GetShapeLIV2().Select(x => x.GetLivY().InverterY()).ToList(), origem_Liv2, cor, 0));


                //LIV3
                linhas.AddRange(Contorno(espessura, cam.GetShapeLIV3().Select(x => x.GetLivY()).ToList(),origem_Liv3 , cor, 0));

            }
            #endregion


            foreach (var fr0 in cam.GetFurosLIV1())
            {
                var nf = Furo2D(espessura, fr0, origem, Brushes.Red.Color);
                linhas.AddRange(nf);
            }

            foreach (var fr0 in cam.GetFurosLIV2())
            {
                if(cam.Faces>2)
                {
                    var nf = Furo2D(espessura, fr0, origem_Liv2, cor);
                    linhas.AddRange(nf);
                }
                else
                {
                    var nf = Furo2D(espessura, fr0.InverterY(), origem_Liv2, cor);
                    linhas.AddRange(nf);
                }
            }


            foreach (var fr0 in cam.GetFurosLIV3())
            {
                var nf = Furo2D(espessura, fr0, origem_Liv3, cor);
                linhas.AddRange(nf);
            }

            foreach(var dob in cam.GetDobras())
            {
                AddDobra(viewPort, espessura, origem, dob);
            }



            foreach (var l in linhas)
            {
                viewPort.Children.Add(l);
            }

            //viewPort.Children.Add(teste);

            var txt = Gera2D.Texto(cam.Descricao, new Ponto3d(cam.GetCentro()));
            viewPort.Children.Add(txt);


            viewPort.ZoomExtents();

        }
        public static void Desenho(List<DLM.cam.Face> cam, HelixViewport3D viewPort)
        {



            double espessura = 1;
            List<LinesVisual3D> linhas = new List<LinesVisual3D>();
            viewPort.Children.Clear();
            viewPort.Children.Add(Gera3d.Luz());
            ControleCamera.Setar(viewPort, ControleCamera.eCameraViews.Top, 0); ;
            Ponto3d origem = new Ponto3d();
            //Brush cor = Brushes.Black.Color;

            foreach (var s in cam)
            {
                linhas.AddRange(Contorno(s.Espessura, s.LivsAninhados().SelectMany(x=> x.SegmentosArco()).ToList(), origem, Conexoes.Utilz.Cores.BrushToColor(s.Cor), s.ContraFlecha));
                foreach (var fr0 in s.Furos)
                {
                    var nf = Furo2D(s.Espessura, fr0, origem, Conexoes.Utilz.Cores.BrushToColor(s.Cor));
                    linhas.AddRange(nf);
                }
                foreach (var dob in s.Dobras)
                {
                    AddDobra(viewPort, espessura, origem, dob);
                }
            }


            foreach (var l in linhas)
            {
                viewPort.Children.Add(l);
            }

            //viewPort.Children.Add(teste);

            //var txt = Gera2D.Texto(cam.Descricao, new Ponto3D(cam.Centro));
            //viewPort.Children.Add(txt);


            viewPort.ZoomExtents();

        }
        private static void AddDobra(HelixViewport3D viewPort, double espessura, Ponto3d origem, Est.Dobra dob)
        {
            var p1 = new Ponto3d(dob.X1, dob.Y1);
            var p2 = new Ponto3d(dob.X2, dob.Y2);

            var s = Linha(espessura, p1, p2, origem, Brushes.DarkGray.Color);
            viewPort.Children.Add(s);
            var t = Texto("Dobra " + dob.Angulo + "°", p1.Centro(p2));
            viewPort.Children.Add(t);
        }
        public static BillboardTextVisual3D Texto(string texto, Ponto3d origem, double tam = 10)
        {
            var teste = new BillboardTextVisual3D();
            teste.Text = texto;
            
            teste.Position = origem.GetPoint3D();
            teste.FontSize = tam;
           
            return teste;
        }
        public static List<LinesVisual3D> Contorno(double espessura, List<Liv> shape,  Ponto3d origem, Color cor,  double ctf)
        {
            List<LinesVisual3D> linhas = new List<LinesVisual3D>();

            /*alguns cams não repetem a coordenada inicial no fim, essa correção adiciona o ponto para fechar o contorno*/
            List<Liv> pts = new List<Liv>();
   
            if(shape.Count>0)
            {
                pts.AddRange(shape);
                pts.Add(shape[0]);
            }

            List<Ponto3d> liv1pts = new List<Ponto3d>();
            if (ctf == 0)
            {

                liv1pts.AddRange(pts.Select(x => new DLM.helix.Util.Ponto3d(x.X, x.Y, 0)));
            }
            else
            {
                var segs = DLM.cam.FuncoesDLMCam.Poligonos.ArcoParaSegmento(pts, ctf);
                liv1pts.AddRange(segs.Select(x => new DLM.helix.Util.Ponto3d(x.X, x.Y, 0)));

            }
            for (int i = 1; i < liv1pts.Count; i++)
            {
                var shp0 = liv1pts[i - 1];
                var shp = liv1pts[i];
                LinesVisual3D l = Linha(espessura, shp0, shp, origem, cor);
                linhas.Add(l);
            }
            return linhas;
        }
        public static List<LinesVisual3D> Furo2D(double espessura, Est.Furo fr0,Ponto3d origem, Color color)
        {
            List<LinesVisual3D> linhas = new List<LinesVisual3D>();
            Furo3d pp = new Furo3d(fr0.Diametro, fr0.X, fr0.Y, fr0.Dist, fr0.Ang);
            var ptsfr = pp.Getcontorno();
            for (int i = 1; i < ptsfr.Count; i++)
            {
                var shp0 = ptsfr[i - 1];
                var shp = ptsfr[i];
                LinesVisual3D l = Linha(espessura, shp0, shp, origem,color);
                linhas.Add(l);
            }
            Linha(espessura, ptsfr[ptsfr.Count - 1], ptsfr[0],origem,color);
            linhas.Add(Linha(espessura, ptsfr[ptsfr.Count - 1], ptsfr[0], origem,color));
            return linhas;
        }
        public static LinesVisual3D Linha(double espessura, Ponto3d shp0, Ponto3d shp, Ponto3d origem, Color cor)
        {
            LinesVisual3D l = new LinesVisual3D();
            l.Color = new Color() { A = cor.A, B = cor.B, G = cor.G, R = cor.R };
            l.Thickness = espessura;
            l.Points.Add(new Point3D(shp0.X + origem.X, shp0.Y + origem.Y, 0 + origem.Z));
            l.Points.Add(new Point3D(shp.X + origem.X, shp.Y + origem.Y, 0 + origem.Z));
            return l;
        }
        public static LinesVisual3D Linha(double espessura, TriangulationPoint shp0, TriangulationPoint shp, Ponto3d origem, Color cor)
        {
            return Linha(espessura,new Ponto3d(shp0.X,shp0.Y,0), new Ponto3d(shp.X, shp.Y, 0),origem, cor);
        }
        public static LinesVisual3D Linha(double espessura, Liv shp0, Liv shp, Ponto3d origem, Color cor)
        {
            return Linha(espessura, new Ponto3d(shp0.X, shp0.Y, 0), new Ponto3d(shp.X, shp.Y, 0), origem, cor);
        }

        public static void AddUCSIcon(HelixViewport3D viewPort, double comp = 100)
        {
            comp = comp / 1000;
            var l1 = Linha(1, new Ponto3d(), new Ponto3d(comp, 0),new Ponto3d(), Colors.Red);
            var l2 = Linha(1, new Ponto3d(), new Ponto3d(0, comp),new Ponto3d(), Colors.Red);
            var xt = Texto("X", new Ponto3d(comp, 0));
            var yt = Texto("Y", new Ponto3d(0, comp));

            viewPort.Children.Add(l1);
            viewPort.Children.Add(l2);
            viewPort.Children.Add(xt);
            viewPort.Children.Add(yt);
        }
    }
}
