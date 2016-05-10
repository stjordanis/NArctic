﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Utilities;
using NumCIL;
using NumCIL.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using MongoDB.Driver;
using NumCIL.Boolean;

namespace NArctic
{
	public static class NumCILMixin
	{
		public static long Dimension<T>(this NdArray<T> array, int axis = 0) {
			return array.Shape.Dimensions [axis].Length;
		}

		public static long Dimension(this NumCIL.Double.NdArray array, int axis = 0) {
			return array.Shape.Dimensions [axis].Length;
		}

		public static NdArray<T> Fill<T>(this NdArray<T> array, Func<long, T> f, int axis = 0, long[] idx=null) {
			idx = idx ?? new long[array.Shape.Dimensions.Length];
			for (idx[axis] = 0; idx[axis] < array.Dimension(axis); idx[axis]++) {
				array.Value [idx] = f (idx [axis]);
			}
			return array;
		}

		public static NdArray<T> Fill<T>(this NdArray<T> array, Func<long,long,T> f, int axis1 = 0, int axis2 = 1, long[] idx=null) {
			idx = idx ?? new long[array.Shape.Dimensions.Length];
			for (idx[axis1] = 0; idx[axis1] < array.Dimension(axis1); idx[axis1]++) 
				for (idx[axis2] = 0; idx[axis2] < array.Dimension(axis2); idx[axis2]++) {
					array.Value [idx] = f (idx [axis1], idx[axis2]);
				}
			return array;
		}
		public static NumCIL.Double.NdArray CumSum(this NumCIL.Double.NdArray array, double start = 0.0) {
			NumCIL.Double.NdArray rtn = array.Copy();
			for(int i=0; i<rtn.Dimension(); i++) {
				start += rtn.Value[i];
				rtn[i] = start;
			}
			return rtn;
		}
	}

	public static class SeriesMixin
	{
		public static Series<double> ToSeries(this NumCIL.Double.NdArray array) {
			return new Series<double> (array);
		}
		public static Series<double> Apply(this Series<double> s, Func<NumCIL.Double.NdArray, NumCIL.Double.NdArray> f)
		{
			return new Series<double> (f(s.Values));
		}

	}

	public abstract class Series : IEnumerable
	{
		public DType DType {get;set;}

		public string Name {
			get { return DType.Name; } 
			set { DType.Name = value; }
		}

		public abstract int Count{ get; }

		public abstract object At (int index);

		public abstract Series this [Range range] {
			get;
		}

		public virtual Series<T> As<T>() {
			var typed = this as Series<T>;
			if(typed==null)
				throw new InvalidCastException();
			return typed;
		}

		public virtual Series<T,Q> As<T,Q>() {
			var typed = this as Series<T,Q>;
			if(typed==null)
				throw new InvalidCastException();
			return typed;
		}

		public Series<DateTime,long> AsDateTime() {
			return this.As<DateTime,long> ();
		}

		public unsafe abstract void  ToBuffer (byte[] buf, DType buftype, int iheight, int icol);

		public unsafe static Series FromBuffer(byte[] buf, DType buftype, int iheight, int icol )
		{
			if (buftype.Fields [icol].Type == typeof(double)) {
				return Series<double>.FromBuffer (buf, buftype, iheight, icol);
			}else if (buftype.Fields[icol].Type == typeof(long)) {
				return Series<long>.FromBuffer (buf, buftype, iheight, icol);
			}else if (buftype.Fields[icol].Type == typeof(DateTime)) {
				return Series<DateTime, long>.FromBuffer (buf, buftype, iheight, icol, DateTime64.ToDateTime, DateTime64.ToDateTime64);
			}else
				throw new InvalidOperationException("Failed decode {0} type".Args(buftype.Fields[icol].Type));
		}

		public abstract Series Clone ();

		IEnumerator IEnumerable.GetEnumerator ()
		{
			for (int i = 0; i < Count; i++)
				yield return this.At (i);
		}

		public static BaseSeries<DateTime> DateTimeRange(int count, DateTime start, DateTime end)
		{
			var delta = (end - start).ToDateTime64 () / count;
			return new Series<DateTime, long> (new NdArray<long>(new Shape(count)).Fill(i=>start.ToDateTime64()+delta*i),
				getter:DateTime64.ToDateTime, 
				setter:DateTime64.ToDateTime64);
		}

		public static Series<double> Random(int count, Randoms.IRandomGenerator gen = null)
		{
			return NArctic.Generate.Random (count, gen).ToSeries();
		}

		public static implicit operator Series(double[] data) 
		{
			return new Series<double>(data);
		}

		public static implicit operator Series(long[] data) 
		{
			return new Series<long> (data);
		}

		public static implicit operator Series(DateTime[] data) 
		{
			return new Series<DateTime, long> (new NdArray<long>(data.Select(DateTime64.ToDateTime64).ToArray()),
				getter:DateTime64.ToDateTime, 
				setter:DateTime64.ToDateTime64);
		}

		public NumCIL.Double.NdArray AsDouble {
			get {
				return new NumCIL.Double.NdArray (As<double>().Values);
			}
			set {
				As<double>().Values = value;		
			}
		}

		public NumCIL.Int64.NdArray AsLong {
			get {
				return new NumCIL.Int64.NdArray (As<long>().Values);
			}
			set {
				As<long>().Values = value;		
			}
		}

		public NumCIL.Int64.NdArray AsDateTime64 {
			get {
				return new NumCIL.Int64.NdArray (As<DateTime,long>(). Source.Values);
			}
			set {
				As<DateTime,long>().Source.Values = value;		
			}
		}

	}

	public abstract class BaseSeries<T> : Series, IList<T> 
	{
		public abstract T this [int index]{ get; set;}

		public BaseSeries(DType dtype){
			DType = dtype;
			if (DType == null) {
				if (typeof(T) == typeof(double))
					DType = DType.Double;
				if (typeof(T) == typeof(long))
					DType = DType.Long;
				if (typeof(T) == typeof(DateTime))
					DType = DType.DateTime64;
				if (DType == null)
					throw new InvalidOperationException ("unknown dtype");
			}
		}

		#region IList<T>
		public virtual IEnumerator<T> GetEnumerator () {
			for (int i = 0; i < Count; i++)
				yield return this [i];
		}

		public int IndexOf (T item)
		{
			throw new NotSupportedException ();
		}

		public void Insert (int index, T item)
		{
			throw new NotSupportedException ();
		}

		public void RemoveAt (int index)
		{
			throw new NotSupportedException ();
		}

		public void Add (T item)
		{
			throw new NotSupportedException ();
		}

		public void Clear ()
		{
			throw new NotSupportedException ();
		}

		public bool Contains (T item)
		{
			throw new NotSupportedException ();
		}

		public void CopyTo (T[] array, int arrayIndex)
		{
			throw new NotSupportedException ();
		}

		public bool Remove (T item)
		{
			throw new NotSupportedException ();
		}

		public bool IsReadOnly {
			get {
				return true;
			}
		}
		#endregion
	}

	public class Series<T, Q> : BaseSeries<T>
	{
		public Series<Q> Source;

		static Q tq(T t) {
			return (Q)(object)t;
		}

		static T qt(Q q) {
			return (T)(object)q;
		}

		protected Func<T,Q> setter = tq;
		protected Func<Q,T> getter = qt;

		public Series(Series<Q> source, Func<Q,T> getter, Func<T,Q> setter, DType dtype=null) 
			: base(dtype)
		{
			Source = source;
			if (setter != null)
				this.setter = setter;
			if (getter != null)
				this.getter = getter;
		}


		public override int Count {
			get{ 
				return Source.Count;
			}
		}

		public override object At (int index)
		{
			return getter (Source[index]);
		}

		public override Series Clone ()
		{
			return new Series<T,Q> (Source.Clone () as Series<Q>, getter, setter, DType);
		}

		public override IEnumerator<T> GetEnumerator() 
		{
			foreach(var q in Source)
				yield return getter(q);
		}

		public override T this[int index] {
			get {
				return getter (Source[index]);
			}
			set { 
				Source[index] = setter(value);
			}
		}

		public override Series this [Range range] {
			get {
				return new Series<T,Q>(Source[range] as Series<Q>,getter,setter);
			}
		}

		public T[] AsArray () {
			return Source.AsArray ().Select (getter).ToArray ();
		}

		public static Series FromBuffer(byte[] buf, DType buftype, int iheight, int icol, Func<Q,T> getter, Func<T,Q> setter )
		{
			return new Series<T,Q> (Series<Q>.FromBuffer(buf, buftype, iheight, icol) as Series<Q>, getter, setter);
		}

		public override void ToBuffer(byte[] buf, DType buftype, int iheight, int icol)
		{
			Source.ToBuffer (buf, buftype, iheight, icol);
		}

		public override string ToString ()
		{
			return "NdArray<{0}>({1}): {2}".Args (typeof(T).Name, Count, " ".Joined (this));
		}

	}

	public class Series<T> : BaseSeries<T>
	{
		public NdArray<T> Values { get; set;}

		public NumCIL.Double.NdArray AsDouble {
			get {
				return new NumCIL.Double.NdArray (Values as NdArray<double>);
			}
		}

		public Series(NdArray<T> values, DType dtype=null) : base(dtype)
		{
			Values = values;
		}

		public Series(T[] data, DType dtype=null) : this(new NdArray<T>(data), dtype)
		{
		}


		public static implicit operator Series<T>(NdArray<T> values) {
			return new Series<T> (values);
		}

		public static implicit operator Series<T>(T[] data) {
			return new Series<T> (new NdArray<T>(data));
		}

		public static implicit operator NdArray<T>(Series<T> series) {
			return series.Values;
		}

		public override int Count
		{
			get{ return (int)Values.Shape.Dimensions [0].Length;}
		}

		public override NArctic.Series Clone()
		{
			return Copy();
		}

		public Series<T> Copy()
		{
			return new Series<T> (this.Values.Clone (), DType);
		}


		public override IEnumerator<T> GetEnumerator() 
		{
			return Values.Value.GetEnumerator ();
		}

		public override T this[int index] {
			get {
				return Values.Value [index];
			}
			set { 
				Values.Value [index] = value;
			}
		}

		public override Series this [Range range] {
			get {
				return new Series<T>(Values[range]);
			}
		}

		public T[] AsArray () {
			return Values.AsArray ();
		}


		public override void ToBuffer(byte[] buf, DType buftype, int iheight, int icol)
		{
			T[] data = this.Values.AsArray();
			buftype.ToBuffer (buf, data, iheight, icol);
		}

		public unsafe static Series FromBuffer(byte[] buf, DType buftype, int iheight, int icol )
		{
			var data = new T[iheight];
			buftype.FromBuffer (buf, data, iheight, icol);
			return new Series<T> (data);
		}

		public override string ToString ()
		{
			return Values.ToString ();
		}

		public override object At(int index) {
			return this [index];
		}


	}
}
