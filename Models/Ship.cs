using System.Collections.Generic;
using System.Drawing;

namespace SeaBattle.Models
{
    public class Ship
    {
        private int _size;
        private List<Point> _positions;
        private List<Point> _hits;

        public Ship(int size)
        {
            _size = size;
            _positions = new List<Point>();
            _hits = new List<Point>();
        }

        public int Size { get { return _size; } }
        public List<Point> Positions { get { return _positions; } }
        public List<Point> Hits { get { return _hits; } }

        public bool IsSunk
        {
            get { return _hits.Count == _size; }
        }

        public bool CheckHit(int x, int y)
        {
            return false;
        }
    }
}