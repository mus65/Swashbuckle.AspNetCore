﻿using System.Reflection;
using System.Xml;
using System.Xml.XPath;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen.Test;
using Swashbuckle.AspNetCore.TestSupport;

namespace Swashbuckle.AspNetCore.SwaggerGen.Benchmark;

[MemoryDiagnoser(true)]
public class XmlCommentsBenchmark
{
    private XmlCommentsDocumentFilter _documentFilter;
    private OpenApiDocument _document;
    private DocumentFilterContext _documentFilterContext;

    private XmlCommentsOperationFilter _operationFilter;
    private OpenApiOperation _operation;
    private OperationFilterContext _operationFilterContext;

    private XmlCommentsParameterFilter _parameterFilter;
    private OpenApiParameter _parameter;
    private ParameterFilterContext _parameterFilterContext;

    private XmlCommentsRequestBodyFilter _requestBodyFilter;
    private OpenApiRequestBody _requestBody;
    private RequestBodyFilterContext _requestBodyFilterContext;

    private const int addMemberCount = 10_000;

    [GlobalSetup]
    public void Setup()
    {
        // Load XML
        XmlDocument xmlDocument;
        using (var xmlComments = File.OpenText($"{typeof(FakeControllerWithXmlComments).Assembly.GetName().Name}.xml"))
        {
            xmlDocument = new XmlDocument();
            xmlDocument.Load(xmlComments);
        }

        // Append dummy members to XML document
        XPathNavigator navigator = xmlDocument.CreateNavigator()!;
        navigator.MoveToRoot();
        navigator.MoveToChild("doc", "");
        navigator.MoveToChild("members", "");

        for (int i = 0; i < addMemberCount; i++)
        {
            navigator.PrependChild($"<member name=\"benchmark_{i}\"></member>");
        }

        MemoryStream xmlStream = new();
        xmlDocument.Save(xmlStream);
        xmlStream.Seek(0, SeekOrigin.Begin);
        XPathDocument xPathDocument = new(xmlStream);

        // Document
        _document = new OpenApiDocument();
        _documentFilterContext = new DocumentFilterContext(
            new[]
            {
                    new ApiDescription
                    {
                        ActionDescriptor = new ControllerActionDescriptor
                        {
                            ControllerTypeInfo = typeof(FakeControllerWithXmlComments).GetTypeInfo(),
                            ControllerName = nameof(FakeControllerWithXmlComments)
                        }
                    },
                    new ApiDescription
                    {
                        ActionDescriptor = new ControllerActionDescriptor
                        {
                            ControllerTypeInfo = typeof(FakeControllerWithXmlComments).GetTypeInfo(),
                            ControllerName = nameof(FakeControllerWithXmlComments)
                        }
                    }
            },
            null,
            null);

        _documentFilter = new XmlCommentsDocumentFilter(xPathDocument);

        // Operation
        _operation = new OpenApiOperation();
        var methodInfo = typeof(FakeConstructedControllerWithXmlComments)
            .GetMethod(nameof(FakeConstructedControllerWithXmlComments.ActionWithSummaryAndResponseTags));
        var apiDescription = ApiDescriptionFactory.Create(methodInfo: methodInfo, groupName: "v1", httpMethod: "POST", relativePath: "resource");
        _operationFilterContext = new OperationFilterContext(apiDescription, null, null, methodInfo);
        _operationFilter = new XmlCommentsOperationFilter(xPathDocument);

        // Parameter
        _parameter = new OpenApiParameter { Schema = new OpenApiSchema { Type = "string", Description = "schema-level description" } };
        var propertyInfo = typeof(XmlAnnotatedType).GetProperty(nameof(XmlAnnotatedType.StringProperty));
        var apiParameterDescription = new ApiParameterDescription { };
        _parameterFilterContext = new ParameterFilterContext(apiParameterDescription, null, null, propertyInfo: propertyInfo);
        _parameterFilter = new XmlCommentsParameterFilter(xPathDocument);

        // Request Body
        _requestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = "string" } }
            }
        };
        var parameterInfo = typeof(FakeControllerWithXmlComments)
            .GetMethod(nameof(FakeControllerWithXmlComments.ActionWithParamTags))!
            .GetParameters()[0];
        var bodyParameterDescription = new ApiParameterDescription
        {
            ParameterDescriptor = new ControllerParameterDescriptor { ParameterInfo = parameterInfo }
        };
        _requestBodyFilterContext = new RequestBodyFilterContext(bodyParameterDescription, null, null, null);
        _requestBodyFilter = new XmlCommentsRequestBodyFilter(xPathDocument);
    }

    [Benchmark]
    public void Document()
    {
        _documentFilter.Apply(_document, _documentFilterContext);
    }

    [Benchmark]
    public void Operation()
    {
        _operationFilter.Apply(_operation, _operationFilterContext);
    }

    [Benchmark]
    public void Parameter()
    {
        _parameterFilter.Apply(_parameter, _parameterFilterContext);
    }

    [Benchmark]
    public void RequestBody()
    {
        _requestBodyFilter.Apply(_requestBody, _requestBodyFilterContext);
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<XmlCommentsBenchmark>();
    }
}