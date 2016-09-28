using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using AVFoundation;
using CoreFoundation;
using CoreGraphics;
using Foundation;
using Photos;
using UIKit;

namespace Chafu
{
    public class PhotoGalleryDataSource : BaseAlbumDataSource, IPHPhotoLibraryChangeObserver
    {
        private readonly AlbumView _albumView;
        private readonly CGSize _cellSize;
        private CGRect _previousPreheatRect = CGRect.Empty;
        private PHAsset _asset;

        public override event EventHandler CameraRollUnauthorized;

        public PHFetchResult Videos { get; set; }
        public PHFetchResult Images { get; set; }
        public ObservableCollection<PHAsset> AllAssets { get; }
        public PHCachingImageManager ImageManager { get; private set; }

        public PhotoGalleryDataSource(AlbumView albumView, CGSize cellSize)
        {
            _albumView = albumView;
            _cellSize = cellSize != CGSize.Empty ? cellSize : new CGSize(100, 100);

            CheckPhotoAuthorization();

            var options = new PHFetchOptions
            {
                SortDescriptors = new[] {new NSSortDescriptor("creationDate", false)}
            };

            Images = PHAsset.FetchAssets(PHAssetMediaType.Image, options);
            Videos = PHAsset.FetchAssets(PHAssetMediaType.Video, options);

            var assets = new List<PHAsset>();
            assets.AddRange(Images.OfType<PHAsset>());
            assets.AddRange(Videos.OfType<PHAsset>());
            AllAssets = new ObservableCollection<PHAsset>(assets.OrderByDescending(a => a.CreationDate.SecondsSinceReferenceDate));
            AllAssets.CollectionChanged += AssetsCollectionChanged;

            PHPhotoLibrary.SharedPhotoLibrary.RegisterChangeObserver(this);
        }

        public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var cell = collectionView.DequeueReusableCell(AlbumViewCell.Key, indexPath) as AlbumViewCell ??
                       new AlbumViewCell();

            if (ImageManager == null) return cell;

            if (cell.Tag != 0)
                ImageManager.CancelImageRequest((int)cell.Tag);

            var asset = AllAssets[(int)indexPath.Item];

            cell.IsVideo = asset.MediaType == PHAssetMediaType.Video;
            cell.Duration = asset.Duration;

            cell.Tag = ImageManager.RequestImageForAsset(asset, _cellSize, PHImageContentMode.AspectFit, null,
                (result, info) => {
                    cell.Image = result;
                    cell.Tag = 0;
                });

            return cell;
        }

        public override nint NumberOfSections(UICollectionView collectionView) => 1;

        public override nint GetItemsCount(UICollectionView collectionView, nint section) => AllAssets?.Count ?? 0;

        public void PhotoLibraryDidChange(PHChange changeInstance)
        {
            DispatchQueue.MainQueue.DispatchAsync(() =>
            {
                //var collectionView = _albumView.CollectionView;
                var imageCollectionChanges = changeInstance.GetFetchResultChangeDetails(Images);
                var videoCollctionChanges = changeInstance.GetFetchResultChangeDetails(Videos);

                if (imageCollectionChanges != null)
                {
                    var imagesBefore = Images;
                    Images = imageCollectionChanges.FetchResultAfterChanges;

                    foreach (var image in imagesBefore.OfType<PHAsset>())
                    {
                        if (!Images.Contains(image))
                            AllAssets.Remove(image);
                    }

                    foreach (var image in Images.OfType<PHAsset>().OrderBy(a => a.CreationDate.SecondsSinceReferenceDate))
                    {
                        if (!AllAssets.Contains(image))
                            AllAssets.Insert(0, image);
                    }
                }

                if (videoCollctionChanges != null)
                {
                    var videosBefore = Images;
                    Videos = videoCollctionChanges.FetchResultAfterChanges;

                    foreach (var video in videosBefore.OfType<PHAsset>())
                    {
                        if (!Videos.Contains(video))
                            AllAssets.Remove(video);
                    }

                    foreach (var video in Videos.OfType<PHAsset>().OrderBy(a => a.CreationDate.SecondsSinceReferenceDate))
                    {
                        if (!AllAssets.Contains(video))
                            AllAssets.Insert(0, video);
                    }
                }
            });
        }

        private void AssetsCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            var collectionView = _albumView.CollectionView;

            if (args.NewItems?.Count > 10 || args.OldItems?.Count > 10)
            {
                collectionView.PerformBatchUpdates(() => {}, null);
                collectionView.ReloadData();
            }
            else if (args.Action == NotifyCollectionChangedAction.Move)
            {
                collectionView.PerformBatchUpdates(() =>
                {
                    var oldCount = args.OldItems.Count;
                    var newCount = args.NewItems.Count;
                    var indexes = new NSIndexPath[oldCount + newCount];

                    var startIndex = args.OldStartingIndex;
                    for (var i = 0; i < oldCount; i++)
                        indexes[i] = NSIndexPath.FromRowSection(startIndex + i, 0);
                    startIndex = args.NewStartingIndex;
                    for (var i = 0; i < oldCount + newCount; i++)
                        indexes[i] = NSIndexPath.FromRowSection(startIndex + i, 0);

                    collectionView.ReloadItems(indexes);
                }, null);
            }
            else if (args.Action == NotifyCollectionChangedAction.Remove)
            {
                collectionView.PerformBatchUpdates(() =>
                {
                    var oldStartingIndex = args.OldStartingIndex;
                    var indexPaths = new NSIndexPath[args.OldItems.Count];
                    for(var i = 0; i < indexPaths.Length; ++i)
                        indexPaths[i] = NSIndexPath.FromRowSection(oldStartingIndex + i, 0);
                    collectionView.DeleteItems(indexPaths);
                }, null);
            }
            else if (args.Action == NotifyCollectionChangedAction.Add)
            {
                collectionView.PerformBatchUpdates(() =>
                {
                    var newStartingIndex = args.NewStartingIndex;
                    var indexPaths = new NSIndexPath[args.NewItems.Count];
                    for (var i = 0; i < indexPaths.Length; ++i)
                        indexPaths[i] = NSIndexPath.FromRowSection(newStartingIndex + i, 0);
                    collectionView.InsertItems(indexPaths);
                }, null);
            }
            else
            {
                collectionView.PerformBatchUpdates(() => {}, null);
                collectionView.ReloadData();
            }

            ResetCachedAssets();
        }

        private void ResetCachedAssets()
        {
            ImageManager?.StopCaching();
            _previousPreheatRect = CGRect.Empty;
        }

        public void UpdateCachedAssets()
        {
            var collectionView = _albumView.CollectionView;

            var preheatRect = collectionView.Bounds;
            preheatRect = CGRect.Inflate(preheatRect, 0.0f, 0.5f*preheatRect.Height);

            var delta = Math.Abs(preheatRect.GetMidY() - _previousPreheatRect.GetMidY());
            if (delta > collectionView.Bounds.Height/3.0)
            {
                var addedIndexPaths = new List<NSIndexPath>();
                var removedIndexPaths = new List<NSIndexPath>();

                var rects = ComputeDifferenceBetweenRect(_previousPreheatRect, preheatRect);

                foreach (var rect in rects.Item1) {
                    var indexPaths = IndexPathsForElementsInRect(collectionView, rect);
                    addedIndexPaths.AddRange(indexPaths);
                }

                foreach (var rect in rects.Item2) {
                    var indexPaths = IndexPathsForElementsInRect(collectionView, rect);
                    removedIndexPaths.AddRange(indexPaths);
                }

                var assetsToStartCaching = AssetsAtIndexPaths(addedIndexPaths);
                var assetsToStopCaching = AssetsAtIndexPaths(removedIndexPaths);

                ImageManager?.StartCaching(assetsToStartCaching, _cellSize, PHImageContentMode.AspectFill,
                    null);
                ImageManager?.StopCaching(assetsToStopCaching, _cellSize, PHImageContentMode.AspectFill,
                    null);

                _previousPreheatRect = preheatRect;
            }
        }

        private static Tuple<IEnumerable<CGRect>, IEnumerable<CGRect>> ComputeDifferenceBetweenRect(
            CGRect oldRect, CGRect newRect)
        {
            if (!newRect.IntersectsWith(oldRect))
                return new Tuple<IEnumerable<CGRect>, IEnumerable<CGRect>>(new[] {newRect}, new[] {oldRect});

            var oldMaxY = oldRect.GetMaxY();
            var oldMinY = oldRect.GetMinY();
            var newMaxY = newRect.GetMaxY();
            var newMinY = newRect.GetMinY();

            var addedRects = new List<CGRect>();
            var removedRects = new List<CGRect>();

            if (newMaxY > oldMaxY) {
                var rectToAdd = new CGRect(newRect.X, oldMaxY, newRect.Width, newMaxY - oldMaxY);
                addedRects.Add(rectToAdd);
            }
            if (oldMinY > newMinY) {
                var rectToAdd = new CGRect(newRect.X, newMinY, newRect.Width, oldMinY - newMinY);
                addedRects.Add(rectToAdd);
            }
            if (newMaxY < oldMaxY) {
                var rectToRemove = new CGRect(newRect.X, newMaxY, newRect.Width, oldMaxY - newMaxY);
                removedRects.Add(rectToRemove);
            }
            if (oldMinY < newMinY) {
                var rectToRemove = new CGRect(newRect.X, oldMinY, newRect.Width, newMinY - oldMinY);
                removedRects.Add(rectToRemove);
            }

            return new Tuple<IEnumerable<CGRect>, IEnumerable<CGRect>>(addedRects, removedRects);
        }

        private static NSIndexPath[] CreateIndexPathArry(int startingPosition, int count)
        {
            var newIndexPaths = new NSIndexPath[count];
            for (var i = 0; i < count; i++)
                newIndexPaths[i] = NSIndexPath.FromRowSection(i + startingPosition, 0);
            return newIndexPaths;
        }

        private static IEnumerable<NSIndexPath> IndexPathsForElementsInRect(UICollectionView collectionView, CGRect rect)
        {
            var allLayoutAttributes = collectionView?.CollectionViewLayout?.LayoutAttributesForElementsInRect(rect);
            if (allLayoutAttributes == null) return new NSIndexPath[0];
            if (allLayoutAttributes.Length == 0) return new NSIndexPath[0];

            return allLayoutAttributes.Select(attribute => attribute.IndexPath);
        }

        private PHAsset[] AssetsAtIndexPaths(IEnumerable<NSIndexPath> indexPaths)
        {
            if (indexPaths == null) return new PHAsset[0];
            var paths = indexPaths.ToArray();
            return !paths.Any() ? 
                new PHAsset[0] : 
                paths.Select(path => AllAssets[(int)path.Item]).ToArray();
        }

        public void CheckPhotoAuthorization()
        {
            PHPhotoLibrary.RequestAuthorization(status =>
            {
                switch (status)
                {
                    case PHAuthorizationStatus.Restricted:
                    case PHAuthorizationStatus.Denied:
                        CameraRollUnauthorized?.Invoke(this, EventArgs.Empty);
                        break;
                    case PHAuthorizationStatus.Authorized:
                        ImageManager = new PHCachingImageManager();
                        if (AllAssets != null && AllAssets.Any())
                            ChangeAsset(AllAssets.First());
                        break;
                }
            });
        }

        public void ChangeAsset(PHAsset asset)
        {
            if (asset == null) return;
            if (_albumView?.ImageCropView == null) return;

            DispatchQueue.MainQueue.DispatchAsync(() =>
            {
                _albumView.ImageCropView.Image = null;
                _asset = asset;
            });

            if (asset.MediaType == PHAssetMediaType.Image)
            {
                DispatchQueue.DefaultGlobalQueue.DispatchAsync(() =>
                {
                    var options = new PHImageRequestOptions { NetworkAccessAllowed = true };
                    var assetSize = new CGSize(asset.PixelWidth, asset.PixelHeight);

                    ImageManager?.RequestImageForAsset(asset, assetSize,
                        PHImageContentMode.AspectFill, options,
                        (result, info) =>
                        {
                            DispatchQueue.MainQueue.DispatchAsync(() =>
                            {
                                CurrentMediaType = ChafuMediaType.Image;
                                _albumView.ImageCropView.Hidden = false;
                                _albumView.MovieView.Hidden = true;
                                _albumView.ImageCropView.ImageSize = assetSize;
                                _albumView.ImageCropView.Image = result;
                            });
                        });
                });
            }
            else if (asset.MediaType == PHAssetMediaType.Video)
            {
                DispatchQueue.DefaultGlobalQueue.DispatchAsync(() =>
                {
                    var options = new PHVideoRequestOptions { NetworkAccessAllowed = true };
                    ImageManager?.RequestAvAsset(asset, options,
                        (ass, mix, info) =>
                        {
                            DispatchQueue.MainQueue.DispatchAsync(() =>
                            {
                                CurrentMediaType = ChafuMediaType.Video;
                                _albumView.ImageCropView.Hidden = true;
                                _albumView.MovieView.Hidden = false;

                                var urlAsset = ass as AVUrlAsset;
                                if (urlAsset == null) return;
                                _albumView.MoviePlayerController.ContentUrl = urlAsset.Url;
                                _albumView.MoviePlayerController.PrepareToPlay();
                            });
                        });
                });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (PHPhotoLibrary.AuthorizationStatus == PHAuthorizationStatus.Authorized)
                    PHPhotoLibrary.SharedPhotoLibrary.UnregisterChangeObserver(this);

                _asset.Dispose();
                _asset = null;
            }

            base.Dispose(disposing);
        }

        public override void GetCroppedImage(Action<UIImage> onImage)
        {
			var scale = UIScreen.MainScreen.Scale;

            var view = _albumView.ImageCropView;

            var normalizedX = view.ContentOffset.X / view.ContentSize.Width;
            var normalizedY = view.ContentOffset.Y / view.ContentSize.Height;

            var normalizedWidth = view.Frame.Width / view.ContentSize.Width;
            var normalizedHeight = view.Frame.Height / view.ContentSize.Height;

            var cropRect = new CGRect(normalizedX, normalizedY, normalizedWidth, normalizedHeight);

            DispatchQueue.DefaultGlobalQueue.DispatchAsync (() => {
				var options = new PHImageRequestOptions {
					DeliveryMode = PHImageRequestOptionsDeliveryMode.HighQualityFormat,
					NetworkAccessAllowed = true,
					NormalizedCropRect = cropRect,
					ResizeMode = PHImageRequestOptionsResizeMode.Exact
				};

				var targetWidth = Math.Floor ((float)_asset.PixelWidth * cropRect.Width);
				var targetHeight = Math.Floor ((float)_asset.PixelHeight * cropRect.Height);
				var dimension = Math.Max (Math.Min (targetHeight, targetWidth), 1024 * scale);

				var targetSize = new CGSize (dimension, dimension);

				PHImageManager.DefaultManager.RequestImageForAsset (_asset, targetSize, PHImageContentMode.AspectFill,
                    options, (result, info) => DispatchQueue.MainQueue.DispatchAsync(() => onImage?.Invoke (result)));
			});
        }

        public override ChafuMediaType CurrentMediaType { get; set; }

        public override void ShowFirstImage()
        {
            ChangeAsset(AllAssets.FirstOrDefault());
            _albumView.CollectionView.ReloadData();
            _albumView.CollectionView.SelectItem(NSIndexPath.FromRowSection(0, 0), false, UICollectionViewScrollPosition.None);
        }

		public override void DeleteCurrentMediaItem()
		{
			throw new NotImplementedException();
		}
	}
}