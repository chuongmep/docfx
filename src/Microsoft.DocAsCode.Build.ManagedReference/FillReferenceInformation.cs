﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Build.ManagedReference
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition;
    using System.Linq;

    using Microsoft.DocAsCode.Build.Common;
    using Microsoft.DocAsCode.DataContracts.Common;
    using Microsoft.DocAsCode.DataContracts.ManagedReference;
    using Microsoft.DocAsCode.Plugins;

    [Export(nameof(ManagedReferenceDocumentProcessor), typeof(IDocumentBuildStep))]
    public class FillReferenceInformation : BaseDocumentBuildStep
    {
        private Dictionary<string, SourceInfo> _items = new Dictionary<string, SourceInfo>();

        public override string Name => nameof(FillReferenceInformation);

        public override int BuildOrder => 0x20;

        public override void Postbuild(ImmutableList<FileModel> models, IHostService host)
        {
            if (models.Count > 0)
            {
                foreach (var model in models)
                {
                    if (model.Type != DocumentType.Article)
                    {
                        continue;
                    }
                    FillCore((PageViewModel)model.Content, host, model.OriginalFileAndType.File);
                }
            }
        }

        #region Private methods

        private void FillCore(PageViewModel model, IHostService host, string file)
        {
            TraceSourceInfo(model, file);
            if (model.References == null || model.References.Count == 0)
            {
                return;
            }
            var uids = new HashSet<string>(GetUidsToFill(model));
            foreach (var r in model.References)
            {
                if (!uids.Contains(r.Uid))
                {
                    continue;
                }
                var m = host.LookupByUid(r.Uid).Find(x => x.Type == DocumentType.Article);
                if (m == null)
                {
                    if (_items.TryGetValue(r.Uid, out SourceInfo i))
                    {
                        FillContent(r, i);
                    }
                    continue;
                }
                var page = (PageViewModel)m.Content;
                var item = page.Items.Find(x => x.Uid == r.Uid);
                if (item == null)
                {
                    continue;
                }
                FillContent(r, item);
            }
        }

        private void FillContent(ReferenceViewModel r, dynamic item)
        {
            if (item.Metadata != null)
            {
                foreach (var pair in item.Metadata)
                {
                    switch (pair.Key)
                    {
                        case Constants.ExtensionMemberPrefix.Spec:
                            break;
                        default:
                            r.Additional[pair.Key] = pair.Value;
                            break;
                    }
                }
            }

            r.Additional["summary"] = item.Summary;
            r.Additional["type"] = item.Type;
            r.Additional["platform"] = item.Platform;
            r.Additional[Constants.PropertyName.IsEii] = item.IsExplicitInterfaceImplementation;
        }

        private void TraceSourceInfo(PageViewModel model, string file)
        {
            foreach (var item in model.Items)
            {
                _items[item.Uid] = new SourceInfo
                {
                    Uid = item.Uid,
                    Summary = item.Summary,
                    Type = item.Type,
                    Syntax = item.Syntax,
                    Platform = item.Platform,
                    File = file,
                    Metadata = item.Metadata,
                };
            }
        }

        private IEnumerable<string> GetUidsToFill(PageViewModel pageViewModel)
        {
            return (from i in pageViewModel.Items
                    from c in (i.Children ?? Enumerable.Empty<string>())
                        .Concat(i.ExtensionMethods ?? Enumerable.Empty<string>())
                        .Concat(i.InheritedMembers ?? Enumerable.Empty<string>())
                    select c);
        }

        #endregion

        private class SourceInfo
        {
            public string Uid { get; set; }
            public string Summary { get; set; }
            public MemberType? Type { get; set; }
            public SyntaxDetailViewModel Syntax { get; set; }
            public List<string> Platform { get; set; }
            public string File { get; set; }
            public Dictionary<string, object> Metadata { get; set; }
            public bool IsExplicitInterfaceImplementation { get; set; }
        }
    }
}
