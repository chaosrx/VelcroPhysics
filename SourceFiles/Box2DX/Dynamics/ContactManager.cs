﻿/*
  Box2DX Copyright (c) 2008 Ihar Kalasouski http://code.google.com/p/box2dx
  Box2D original C++ version Copyright (c) 2006-2007 Erin Catto http://www.gphysics.com

  This software is provided 'as-is', without any express or implied
  warranty.  In no event will the authors be held liable for any damages
  arising from the use of this software.

  Permission is granted to anyone to use this software for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

  1. The origin of this software must not be misrepresented; you must not
     claim that you wrote the original software. If you use this software
     in a product, an acknowledgment in the product documentation would be
     appreciated but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
     misrepresented as being the original software.
  3. This notice may not be removed or altered from any source distribution.
*/

using System;
using System.Collections.Generic;
using System.Text;

using Box2DX.Common;
using Box2DX.Collision;

namespace Box2DX.Dynamics
{
	/// <summary>
	// Delegate of World.
	/// </summary>
	public class ContactManager : PairCallback
	{
		public World _world;

		// This lets us provide broadphase proxy pair user data for
		// contacts that shouldn't exist.
		public NullContact _nullContact;

		public bool _destroyImmediate;

		public ContactManager()
		{
			_world = null;
			_destroyImmediate = false;
		}

		// This is a callback from the broadphase when two AABB proxies begin
		// to overlap. We create a Contact to manage the narrow phase.
		public override object PairAdded(object proxyUserData1, object proxyUserData2)
		{
			Fixture fixtureA = proxyUserData1 as Fixture;
			Fixture fixtureB = proxyUserData2 as Fixture;

			Body body1 = fixtureA.GetBody();
			Body body2 = fixtureB.GetBody();

			if (body1.IsStatic() && body2.IsStatic())
			{
				return _nullContact;
			}

			if (fixtureA.GetBody() == fixtureB.GetBody())
			{
				return _nullContact;
			}

			if (body2.IsConnected(body1))
			{
				return _nullContact;
			}

			if (_world._contactFilter != null && _world._contactFilter.ShouldCollide(fixtureA, fixtureB) == false)
			{
				return _nullContact;
			}

			// Call the factory.
			Contact c = Contact.Create(fixtureA, fixtureB);

			if (c == null)
			{
				return _nullContact;
			}

			// Contact creation may swap shapes.
			fixtureA = c.GetFixtureA();
			fixtureB = c.GetFixtureB();
			body1 = fixtureA.GetBody();
			body2 = fixtureB.GetBody();

			// Insert into the world.
			c.Prev = null;
			c.Next = _world._contactList;
			if (_world._contactList != null)
			{
				_world._contactList.Prev = c;
			}
			_world._contactList = c;

			// Connect to island graph.

			// Connect to body 1
			c.NodeA.Contact = c;
			c.NodeA.Other = body2;

            c.NodeA.Prev = null;
            c.NodeA.Next = body1._contactList;
			if (body1._contactList != null)
			{
                body1._contactList.Prev = c.NodeA;
			}
            body1._contactList = c.NodeA;

			// Connect to body 2
            c.NodeB.Contact = c;
            c.NodeB.Other = body1;

            c.NodeB.Prev = null;
            c.NodeB.Next = body2._contactList;
			if (body2._contactList != null)
			{
                body2._contactList.Prev = c.NodeB;
			}
            body2._contactList = c.NodeB;

			++_world._contactCount;
			return c;
		}

		// This is a callback from the broadphase when two AABB proxies cease
		// to overlap. We retire the Contact.
		public override void PairRemoved(object proxyUserData1, object proxyUserData2, object pairUserData)
		{
			//B2_NOT_USED(proxyUserData1);
			//B2_NOT_USED(proxyUserData2);

			if (pairUserData == null)
			{
				return;
			}

			Contact c = pairUserData as Contact;
			if (c == _nullContact)
			{
				return;
			}

			// An attached body is being destroyed, we must destroy this contact
			// immediately to avoid orphaned shape pointers.
			Destroy(c);
		}

		public void Destroy(Contact c)
		{
			Fixture fixtureA = c.GetFixtureA();
			Fixture fixtureB = c.GetFixtureB();
			Body body1 = fixtureA.GetBody();
			Body body2 = fixtureB.GetBody();

            if (c.Manifold.PointCount > 0)
            {
                _world._contactListener.EndContact(c);
            }

			// Remove from the world.
			if (c.Prev != null)
			{
				c.Prev.Next = c.Next;
			}

			if (c.Next != null)
			{
				c.Next.Prev = c.Prev;
			}

			if (c == _world._contactList)
			{
				_world._contactList = c.Next;
			}

			// Remove from body 1
            if (c.NodeA.Prev != null)
			{
                c.NodeA.Prev.Next = c.NodeA.Next;
			}

            if (c.NodeA.Next != null)
			{
                c.NodeA.Next.Prev = c.NodeA.Prev;
			}

            if (c.NodeA == body1._contactList)
			{
                body1._contactList = c.NodeA.Next;
			}

			// Remove from body 2
            if (c.NodeB.Prev != null)
			{
                c.NodeB.Prev.Next = c.NodeB.Next;
			}

            if (c.NodeB.Next != null)
			{
                c.NodeB.Next.Prev = c.NodeB.Prev;
			}

            if (c.NodeB == body2._contactList)
			{
                body2._contactList = c.NodeB.Next;
			}

			// Call the factory.
			Contact.Destroy(c);
			--_world._contactCount;
		}

		// This is the top level collision call for the time step. Here
		// all the narrow phase collision is processed for the world
		// contact list.
		public void Collide()
		{
			// Update awake contacts.
			for (Contact c = _world._contactList; c != null; c = c.GetNext())
			{
				Body body1 = c.GetFixtureA().GetBody();
				Body body2 = c.GetFixtureB().GetBody();
				if (body1.IsSleeping() && body2.IsSleeping())
				{
					continue;
				}

				c.Update(_world._contactListener);
			}
		}
	}
}