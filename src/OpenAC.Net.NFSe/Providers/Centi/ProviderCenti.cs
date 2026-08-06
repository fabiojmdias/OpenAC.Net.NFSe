// ***********************************************************************
// Assembly         : OpenAC.Net.NFSe
// Author           : Fabio Dias (ZCoder Sistemas)
// Created          : 08-01-2025
//
// ***********************************************************************
// <copyright file="ProviderBase.cs" company="OpenAC .Net">
//		        		   The MIT License (MIT)
//	     		Copyright (c) 2014 - 2025 Projeto OpenAC .Net
//
//	 Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//	 The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//	 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
// </copyright>
// <summary></summary>
// ***********************************************************************

using OpenAC.Net.Core.Extensions;
using OpenAC.Net.DFe.Core;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;
using OpenAC.Net.NFSe.Commom.Interface;
using OpenAC.Net.NFSe.Commom.Model;
using OpenAC.Net.NFSe.Commom.Types;
using OpenAC.Net.NFSe.Configuracao;
using OpenAC.Net.NFSe.Nota;
using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace OpenAC.Net.NFSe.Providers.Centi;

internal sealed class ProviderCenti : ProviderABRASF200
{
    public ProviderCenti(ConfigNFSe config, OpenMunicipioNFSe municipio) : base(config, municipio)
    {
        Name = "Centi";
        Versao = VersaoNFSe.ve200;
    }

    protected override IServiceClient GetClient(TipoUrl tipo) => new CentiServiceClient(this, tipo);

    protected override string GetNamespace() => "xmlns=\"http://www.centi.com.br/files/nfse.xsd\"";

    protected override void PrepararEnviarSincrono(RetornoEnviar retornoWebservice, NotaServicoCollection notas)
    {
        if (retornoWebservice.Lote == 0) retornoWebservice.Erros.Add(new EventoRetorno { Codigo = "0", Descricao = "Lote não informado." });
        if (notas.Count == 0) retornoWebservice.Erros.Add(new EventoRetorno { Codigo = "0", Descricao = "RPS não informado." });
        if (retornoWebservice.Erros.Any()) return;
        var xmlLoteRps = new StringBuilder();
        foreach (var nota in notas)
        {
            var xmlRps = WriteXmlRps(nota, false, false);
            xmlLoteRps.Append(xmlRps);
            GravarRpsEmDisco(xmlRps, $"Rps-{nota.IdentificacaoRps.DataEmissao:yyyyMMdd}-{nota.IdentificacaoRps.Numero}.xml", nota.IdentificacaoRps.DataEmissao);
        }

        var xmlLote = new StringBuilder();
        xmlLote.Append($"<GerarNfseEnvio {GetNamespace()}>");
        xmlLote.Append(xmlLoteRps);
        xmlLote.Append("</GerarNfseEnvio>");
        retornoWebservice.XmlEnvio = xmlLote.ToString();
    }

    protected override void AssinarEnviarSincrono(RetornoEnviar retornoWebservice)
    {
        retornoWebservice.XmlEnvio = XmlSigning.AssinarXmlTodos(retornoWebservice.XmlEnvio, "Rps", "", Certificado);
    }

    protected override void TratarRetornoConsultarNFSeRps(RetornoConsultarNFSeRps retornoWebservice, NotaServicoCollection notas)
    {
        // Analisa mensagem de retorno
        var xmlRet = XDocument.Parse(retornoWebservice.XmlRetorno);
        MensagemErro(retornoWebservice, xmlRet, "GerarNfseResposta");
        if (retornoWebservice.Erros.Any()) return;

        var elResposta = xmlRet.ElementAnyNs("GerarNfseResposta");
        if (elResposta == null)
        {
            retornoWebservice.Erros.Add(new EventoRetorno { Codigo = "0", Descricao = "Nota Fiscal não encontrada! (CompNfse)" });
            return;
        }
        var compNfse = elResposta?.ElementAnyNs("CompNfse") ?? elResposta?.ElementAnyNs("ListaNfse")?.ElementAnyNs("CompNfse");
        if (compNfse == null)
        {
            retornoWebservice.Erros.Add(new EventoRetorno { Codigo = "0", Descricao = "Nota Fiscal não encontrada! (CompNfse)" });
            return;
        }

        var nfse = compNfse.ElementAnyNs("Nfse").ElementAnyNs("InfNfse");
        var numeroNFSe = nfse.ElementAnyNs("Numero")?.GetValue<string>() ?? string.Empty;
        var chaveNFSe = nfse.ElementAnyNs("CodigoVerificacao")?.GetValue<string>() ?? string.Empty;
        var dataNFSe = nfse.ElementAnyNs("DataEmissao")?.GetValue<DateTime>() ?? DateTime.Now;
        var numeroRps = nfse.ElementAnyNs("IdentificacaoRps").GetValue<string>() ?? string.Empty;

        GravarNFSeEmDisco(compNfse.AsString(true), $"NFSe-{numeroNFSe}-{chaveNFSe}-.xml", dataNFSe);

        // Carrega a nota fiscal na coleção de Notas Fiscais
        var nota = notas.FirstOrDefault(x => x.IdentificacaoRps.Numero == numeroRps);

        if (nota == null)
        {
            nota = notas.Load(compNfse.ToString());
        }
        else
        {
            nota.IdentificacaoNFSe.Numero = numeroNFSe;
            nota.IdentificacaoNFSe.Chave = chaveNFSe;
            nota.IdentificacaoNFSe.DataEmissao = dataNFSe;
            nota.XmlOriginal = compNfse.ToString();

            var nfseCancelamento = compNfse.ElementAnyNs("NfseCancelamento");

            if (nfseCancelamento != null)
            {
                nota.Situacao = SituacaoNFSeRps.Cancelado;

                var confirmacaoCancelamento = nfseCancelamento
                    .ElementAnyNs("Confirmacao");

                if (confirmacaoCancelamento != null)
                {
                    var pedido = confirmacaoCancelamento.ElementAnyNs("Pedido");

                    if (pedido != null)
                    {
                        var codigoCancelamento = pedido
                            .ElementAnyNs("InfPedidoCancelamento")
                            .ElementAnyNs("CodigoCancelamento")
                            .GetValue<string>();

                        nota.Cancelamento.Pedido.CodigoCancelamento = codigoCancelamento;

                        nota.Cancelamento.Signature = DFeSignature.Load(pedido.ElementAnyNs("Signature").ToString());
                    }
                }

                nota.Cancelamento.DataHora = confirmacaoCancelamento
                    .ElementAnyNs("DataHora")
                    .GetValue<DateTime>();
            }
        }
        retornoWebservice.Nota = nota;
        retornoWebservice.Sucesso = true;
    }

    protected override void TratarRetornoEnviarSincrono(RetornoEnviar retornoWebservice, NotaServicoCollection notas)
    {
        // Analisa mensagem de retorno
        var xmlRet = XDocument.Parse(retornoWebservice.XmlRetorno);
        MensagemErro(retornoWebservice, xmlRet, "GerarNfseResposta");
        if (retornoWebservice.Erros.Any()) return;
        retornoWebservice.Data = DateTime.Now;
        var listaNfse = xmlRet.Root.ElementAnyNs("ListaNfse");
        if (listaNfse == null)
        {
            retornoWebservice.Erros.Add(new EventoRetorno { Codigo = "0", Descricao = "Lista de NFSe não encontrada! (ListaNfse)" });
            return;
        }

        foreach (var compNfse in listaNfse.ElementsAnyNs("CompNfse"))
        {
            var nfse = compNfse.ElementAnyNs("Nfse").ElementAnyNs("InfNfse");
            var numeroNFSe = nfse.ElementAnyNs("Numero")?.GetValue<string>() ?? string.Empty;
            var chaveNFSe = nfse.ElementAnyNs("CodigoVerificacao")?.GetValue<string>() ?? string.Empty;
            var dataNFSe = nfse.ElementAnyNs("DataEmissao")?.GetValue<DateTime>() ?? DateTime.Now;
            var numeroRps = nfse.ElementAnyNs("IdentificacaoRps").GetValue<string>() ?? string.Empty;
            GravarNFSeEmDisco(compNfse.AsString(true), $"NFSe-{numeroNFSe}-{chaveNFSe}-.xml", dataNFSe);

            var nota = notas.FirstOrDefault(x => x.IdentificacaoRps.Numero == numeroRps);
            if (nota == null)
            {
                nota = notas.Load(compNfse.ToString());
            }
            else
            {
                nota.IdentificacaoNFSe.Numero = numeroNFSe;
                nota.IdentificacaoNFSe.Chave = chaveNFSe;
                nota.IdentificacaoNFSe.DataEmissao = dataNFSe;
                nota.XmlOriginal = compNfse.ToString();
            }
        }
        retornoWebservice.Sucesso = true;
    }

    protected override XElement WritePrestadorRps(NotaServico nota)
    {
        if (nota.Prestador.CpfCnpj.IsEmpty() && nota.Prestador.InscricaoMunicipal.IsEmpty()) return null;
        var prestador = new XElement("Prestador");
        var cpfCnpjPrestador = new XElement("CpfCnpj");
        prestador.Add(cpfCnpjPrestador);
        cpfCnpjPrestador.AddChild(AdicionarTagCNPJCPF("", "Cpf", "Cnpj", nota.Prestador.CpfCnpj));
        if (nota.ConstrucaoCivil != null && !string.IsNullOrEmpty(nota.ConstrucaoCivil?.LogradouroObra))
        {
            var dadosobra = new XElement("DadosObra");
            dadosobra.AddChild(AddTag(TipoCampo.Str, "", "Cno", 1, 125, Ocorrencia.NaoObrigatoria, nota.ConstrucaoCivil.CnoObra));
            dadosobra.AddChild(AddTag(TipoCampo.Str, "", "CepObra", 1, 8, Ocorrencia.NaoObrigatoria, nota.ConstrucaoCivil.CepObra));
            dadosobra.AddChild(AddTag(TipoCampo.Str, "", "LogradouroObra", 1, 125, Ocorrencia.NaoObrigatoria, nota.ConstrucaoCivil.LogradouroObra));
            dadosobra.AddChild(AddTag(TipoCampo.Str, "", "NumeroObra", 1, 10, Ocorrencia.NaoObrigatoria, nota.ConstrucaoCivil.NumeroObra));
            dadosobra.AddChild(AddTag(TipoCampo.Str, "", "BairroObra", 1, 60, Ocorrencia.NaoObrigatoria, nota.ConstrucaoCivil.BairroObra));
            dadosobra.AddChild(AddTag(TipoCampo.Str, "", "ComplementoObra", 1, 60, Ocorrencia.NaoObrigatoria, nota.ConstrucaoCivil.ComplementoObra));
            prestador.Add(dadosobra);
        }
        prestador.AddChild(AddTag(TipoCampo.Str, "", "InscricaoMunicipal", 1, 15, Ocorrencia.NaoObrigatoria, nota.Prestador.InscricaoMunicipal));
        return prestador;
    }

    protected override XElement WriteServicosRps(NotaServico nota)
    {
        var servico = new XElement("Servico");
        servico.Add(WriteValoresRps(nota));
        servico.AddChild(AddTag(TipoCampo.Int, "", "IssRetido", 1, 1, Ocorrencia.Obrigatoria, nota.Servico.Valores.IssRetido == SituacaoTributaria.Retencao ? 1 : 2));
        if (nota.Servico.ResponsavelRetencao.HasValue)
            servico.AddChild(AddTag(TipoCampo.Int, "", "ResponsavelRetencao", 1, 1, Ocorrencia.NaoObrigatoria, (int)nota.Servico.ResponsavelRetencao + 1));
        servico.AddChild(AddTag(TipoCampo.Str, "", "ItemListaServico", 1, 5, Ocorrencia.Obrigatoria, nota.Servico.ItemListaServico));
        servico.AddChild(AddTag(TipoCampo.Str, "", "CodigoNbs", 1, 9, Ocorrencia.NaoObrigatoria, nota.Servico.CodigoNbs));
        servico.AddChild(AddTag(TipoCampo.Str, "", "CodigoINDOP", 1, 6, Ocorrencia.NaoObrigatoria, nota.Servico.CodigoIndicadorOperacao));
        servico.AddChild(AddTag(TipoCampo.Str, "", "CodigoClassTrib", 1, 6, Ocorrencia.NaoObrigatoria, nota.Servico.CodigoClassificacaoTributaria));
        servico.AddChild(AddTag(TipoCampo.Str, "", "CodigoCnae", 1, 7, Ocorrencia.NaoObrigatoria, nota.Servico.CodigoCnae));
        servico.AddChild(AddTag(TipoCampo.Str, "", "CodigoTributacaoMunicipio", 1, 20, Ocorrencia.NaoObrigatoria, nota.Servico.CodigoTributacaoMunicipio));
        servico.AddChild(AddTag(TipoCampo.Str, "", "Discriminacao", 1, 2000, Ocorrencia.Obrigatoria, nota.Servico.Discriminacao));
        servico.AddChild(AddTag(TipoCampo.Str, "", "CodigoMunicipio", 1, 20, Ocorrencia.Obrigatoria, nota.Servico.CodigoMunicipio));
        servico.AddChild(AddTag(TipoCampo.Int, "", "CodigoPais", 4, 4, Ocorrencia.MaiorQueZero, nota.Servico.CodigoPais));
        servico.AddChild(AddTag(TipoCampo.Int, "", "ExigibilidadeISS", 1, 1, Ocorrencia.Obrigatoria, (int)nota.Servico.ExigibilidadeIss + 1));
        servico.AddChild(AddTag(TipoCampo.Int, "", "MunicipioIncidencia", 7, 7, Ocorrencia.MaiorQueZero, nota.Servico.MunicipioIncidencia));
        servico.AddChild(AddTag(TipoCampo.Str, "", "NumeroProcesso", 1, 30, Ocorrencia.NaoObrigatoria, nota.Servico.NumeroProcesso));

        return servico;
    }
}