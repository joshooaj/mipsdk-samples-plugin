﻿using System;
using VideoOS.Platform.Client;
using VideoOS.Platform.UI.Controls;

namespace SCWorkSpace.Client
{
    public class SCWorkSpaceViewItemPlugin : ViewItemPlugin
    {
        internal protected static VideoOSIconSourceBase _treeNodeImage;

        public SCWorkSpaceViewItemPlugin()
        {
            _treeNodeImage = new VideoOSIconUriSource { Uri = new Uri("pack://application:,,,/SCWorkSpace;component/Resources/WorkSpace.png") };
        }

        public override Guid Id
        {
            get { return new Guid("CCD3FD1E-FD65-4F3D-9012-1BE73D44D185"); }
        }

        public override VideoOSIconSourceBase IconSource
        {
            get { return _treeNodeImage; }
        }

        public override string Name
        {
            get { return "WorkSpace Plugin View Item"; }
        }

        public override bool HideSetupItem
        {
            get
            {
                return false;
            }
        }

        public override ViewItemManager GenerateViewItemManager()
        {
            return new SCWorkSpaceViewItemManager();
        }

        public override void Init()
        {
        }

        public override void Close()
        {
        }
    }
}
