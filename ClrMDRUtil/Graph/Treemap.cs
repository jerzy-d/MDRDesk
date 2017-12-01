using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    public struct Container
    {
        double _xoffset;
        double _yoffset;
        double _height;
        double _width;

        public Container(double xoffset, double yoffset, double height, double width)
        {
            _xoffset = xoffset;
            _yoffset = yoffset;
            _height = height;
            _width = width;
        }

        public double ShortestEdge()
        {
            return Math.Min(_height, _width);
        }


        // getCoordinates - for a row of boxes which we've placed 
        //                  return an array of their cartesian coordinates
        public List<double[]> GetCoordinates(List<double> row)
        {
            var rowlen = row.Count;
            var coordinates = new List<double[]>(row.Count);
            double subxoffset = _xoffset, subyoffset = _yoffset; //our offset within the container
            var sum = Treemap.Sum(row);
            var areawidth = sum / _height;
            var areaheight = sum / _width;

            if (_width >= _height)
            {
                for (int i = 0; i < rowlen; ++i)
                {
                    coordinates.Add(new double[] { subxoffset, subyoffset, subxoffset + areawidth, subyoffset + row[i] / areawidth });
                    subyoffset = subyoffset + row[i] / areawidth;
                }
            }
            else
            {
                for (int i = 0; i<rowlen; ++i)
                {
                    coordinates.Add(new double[] { subxoffset, subyoffset, subxoffset + row[i] / areaheight, subyoffset + areaheight });
                    subxoffset = subxoffset + row[i] / areaheight;
                }
            }
            return coordinates;
        }

        // cutArea - once we've placed some boxes into an row we then need to identify the remaining area, 
        //           this function takes the area of the boxes we've placed and calculates the location and
        //           dimensions of the remaining space and returns a container box defined by the remaining area
        
        public Container CutArea(double area)
        {
            if (_width >= _height)
            {
                var areawidth = area / _height;
                var newwidth = _width - areawidth;
                return new Container(_xoffset + areawidth, _yoffset, newwidth, _height);
            }
            else
            {
                var areaheight = area / _width;
                var newheight = _height - areaheight;
                return  new Container(_xoffset, _yoffset + areaheight, _width, newheight);
            }
        }
    }


    public class Treemap
    {

        // squarify  - as per the Bruls paper 
        //             plus coordinates stack and containers so we get 
        //             usable data out of it 
        public static Stack<List<double[]>> Squarify(double[] data, int curNdx, List<double> currentrow, Container container, Stack<List<double[]>> stack)
        {
            if (data.Length == curNdx)
            {
                stack.Push(container.GetCoordinates(currentrow));
                return stack;
            }

            var length = container.ShortestEdge();
            var nextdatapoint = data[curNdx];

            if (ImprovesRatio(currentrow, nextdatapoint, length))
            {
                currentrow.Add(nextdatapoint);


                Squarify(data, curNdx+1, currentrow, container, stack);
            }
            else
            {
                var newcontainer = container.CutArea(Sum(currentrow));
                stack.Push(container.GetCoordinates(currentrow));
                Squarify(data, curNdx, new List<double>(), newcontainer, stack);
            }
            return stack;
        }


        private static double[] Normalize(double[] data, double area)
        {
            var datalen = data.Length;
            var normalizeddata = new double[datalen];
            var sum = 0.0;
            for (int i = 0; i < datalen; ++i)
            {
                sum += data[i];
            }
            var multiplier = area / sum;
            for (int i = 0; i < datalen; ++i)
            {
                normalizeddata[i] = data[i] * multiplier;
            }
            return normalizeddata;
        }

        // improveRatio - implements the worse calculation and comparision as given in Bruls
        //                (note the error in the original paper; fixed here) 
        static bool ImprovesRatio(List<double> currentrow, double nextnode, double length)
        {
            if (currentrow.Count == 0)
            {
                return true;
            }

            var newrow = new List<double>(currentrow);
            newrow.Add(nextnode);

            var currentratio = CalculateRatio(currentrow, length);
            var newratio = CalculateRatio(newrow, length);

            // the pseudocode in the Bruls paper has the direction of the comparison
            // wrong, this is the correct one.
            return currentratio >= newratio;
        }

        private static double CalculateRatio(IList<double> row, double length)
        {
            (double min, double max, double sum) = MinMaxSum(row);
            return Math.Max((length* length) * max / (sum*sum), (sum*sum) / ((length*length) * min));
        }

        public static double Sum(IList<double> ary)
        {
            double sum = 0.0;
            for (int i = 0, icnt = ary.Count; i < icnt; ++i)
            {
                sum += ary[i];
            }
            return sum;
        }
        public static ValueTuple<double,double,double> MinMaxSum(IList<double> ary)
        {
            double min = double.MaxValue;
            double max = double.MinValue;
            double sum = 0.0;
            for (int i = 0, icnt = ary.Count; i < icnt; ++i)
            {
                var val = ary[i];
                if (val > min) min = val;
                if (val > max) max = val;
                sum += val;
            }
            return (min,max,sum);
        }

    }
}

/*
# Squarified Treemap Layout
# Implements algorithm from Bruls, Huizing, van Wijk, "Squarified Treemaps"
#   (but not using their pseudocode)

def normalize_sizes(sizes, dx, dy):
    total_size = sum(sizes)
    total_area = dx* dy
    sizes = map(float, sizes)
    sizes = map(lambda size: size* total_area / total_size, sizes)
    return list(sizes)

def pad_rectangle(rect):
    if rect['dx'] > 2:
        rect['x'] += 1
        rect['dx'] -= 2
    if rect['dy'] > 2:
        rect['y'] += 1
        rect['dy'] -= 2

def layoutrow(sizes, x, y, dx, dy):
    # generate rects for each size in sizes
    # dx >= dy
    # they will fill up height dy, and width will be determined by their area
    # sizes should be pre-normalized wrt dx * dy (i.e., they should be same units)
    covered_area = sum(sizes)
    width = covered_area / dy
    rects = []
    for size in sizes:
        rects.append({'x': x, 'y': y, 'dx': width, 'dy': size / width})
        y += size / width
    return rects

def layoutcol(sizes, x, y, dx, dy):
    # generate rects for each size in sizes
    # dx < dy
    # they will fill up width dx, and height will be determined by their area
    # sizes should be pre-normalized wrt dx * dy (i.e., they should be same units)
    covered_area = sum(sizes)
    height = covered_area / dx
    rects = []
    for size in sizes:
        rects.append({'x': x, 'y': y, 'dx': size / height, 'dy': height})
        x += size / height
    return rects

def layout(sizes, x, y, dx, dy):
    return layoutrow(sizes, x, y, dx, dy) if dx >= dy else layoutcol(sizes, x, y, dx, dy)

def leftoverrow(sizes, x, y, dx, dy):
    # compute remaining area when dx >= dy
    covered_area = sum(sizes)
    width = covered_area / dy
    leftover_x = x + width
    leftover_y = y
    leftover_dx = dx - width
    leftover_dy = dy
    return (leftover_x, leftover_y, leftover_dx, leftover_dy)

def leftovercol(sizes, x, y, dx, dy):
    # compute remaining area when dx >= dy
    covered_area = sum(sizes)
    height = covered_area / dx
    leftover_x = x
    leftover_y = y + height
    leftover_dx = dx
    leftover_dy = dy - height
    return (leftover_x, leftover_y, leftover_dx, leftover_dy)

def leftover(sizes, x, y, dx, dy):
    return leftoverrow(sizes, x, y, dx, dy) if dx >= dy else leftovercol(sizes, x, y, dx, dy)

def worst_ratio(sizes, x, y, dx, dy):
    return max([max(rect['dx'] / rect['dy'], rect['dy'] / rect['dx']) for rect in layout(sizes, x, y, dx, dy)])

def squarify(sizes, x, y, dx, dy):
    # sizes should be pre-normalized wrt dx * dy (i.e., they should be same units)
    # or dx * dy == sum(sizes)
    # sizes should be sorted biggest to smallest
    sizes = list(map(float, sizes))
    
    if len(sizes) == 0:
        return []
    
    if len(sizes) == 1:
        return layout(sizes, x, y, dx, dy)

    # figure out where 'split' should be
i = 1
    while i<len(sizes) and worst_ratio(sizes[:i], x, y, dx, dy) >= worst_ratio(sizes[:(i + 1)], x, y, dx, dy):
        i += 1
    current = sizes[:i]
    remaining = sizes[i:]

    (leftover_x, leftover_y, leftover_dx, leftover_dy) = leftover(current, x, y, dx, dy)
    return layout(current, x, y, dx, dy) + \
            squarify(remaining, leftover_x, leftover_y, leftover_dx, leftover_dy)

def padded_squarify(sizes, x, y, dx, dy):
    rects = squarify(sizes, x, y, dx, dy)
    for rect in rects:
        pad_rectangle(rect)
    return rects

def plot(sizes, norm_x= 100, norm_y= 100,
         color= None, label= None, value= None,
         ax= None, ** kwargs):

    """
    Plotting with Matplotlib.

    Parameters
    ----------
    sizes: input for squarify
    norm_x, norm_y: x and y values for normalization
    color: color string or list-like(see Matplotlib documentation for details)
    label: list-like used as label text
    value: list-like used as value text(in most cases identical with sizes argument)
    ax: Matplotlib Axes instance
    kwargs: dict, keyword arguments passed to matplotlib.Axes.bar

    Returns
    -------
    axes: Matplotlib Axes
    """
    
    import matplotlib.pyplot as plt

    if ax is None:
        ax = plt.gca()

    if color is None:
        import matplotlib.cm
        import random
        cmap = matplotlib.cm.get_cmap()
        color = [cmap(random.random()) for i in range(len(sizes))]

    normed = normalize_sizes(sizes, norm_x, norm_y)
    rects = squarify(normed, 0, 0, norm_x, norm_y)


    x = [rect['x'] for rect in rects]
    y = [rect['y'] for rect in rects]
    dx = [rect['dx'] for rect in rects]
    dy = [rect['dy'] for rect in rects]

    ax.bar(x, dy, width= dx, bottom= y, color= color,
       label= label, align= 'edge', ** kwargs)

    if not value is None:
        va = 'center' if label is None else 'top'
            
        for v, r in zip(value, rects):
            x, y, dx, dy = r['x'], r['y'], r['dx'], r['dy']
            ax.text(x + dx / 2, y + dy / 2, v, va= va, ha= 'center')

    if not label is None:
        va = 'center' if value is None else 'bottom'
        for l, r in zip(label, rects):
            x, y, dx, dy = r['x'], r['y'], r['dx'], r['dy']
            ax.text(x + dx / 2, y + dy / 2, l, va= va, ha= 'center')

    ax.set_xlim(0, norm_x)
    ax.set_ylim(0, norm_y)
    return ax
*/