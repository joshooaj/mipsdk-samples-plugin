﻿using System;
using VideoOS.Platform.Client;
using VideoOS.Platform.UI.Controls;

namespace SCWorkSpace.Client
{
    public class SCWorkSpaceViewItemPlugin2 : ViewItemPlugin
    {
        internal protected static VideoOSIconSourceBase _treeNodeImage;

        public SCWorkSpaceViewItemPlugin2()
        {
            _treeNodeImage = new VideoOSIconUriSource { Uri = new Uri("pack://application:,,,/SCWorkSpace;component/Resources/WorkSpace.png") };
        }

        public override Guid Id
        {
            get { return new Guid("CCD3FD1E-FD65-4F3D-9012-1BE73D44D188"); }
        }

        public override VideoOSIconSourceBase IconSource
        {
            get { return _treeNodeImage; }
        }

        public override string Name
        {
            get { return "WorkSpace Plugin View Item2"; }
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
            return new SCWorkSpaceViewItemManager2();
        }

        public override void Init()
        {
        }

        public override void Close()
        {
        }
    }
}
